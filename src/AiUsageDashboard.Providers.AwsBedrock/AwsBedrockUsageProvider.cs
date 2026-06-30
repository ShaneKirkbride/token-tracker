using AiUsageDashboard.Contracts;
using AiUsageDashboard.Core;
using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiUsageDashboard.Providers.AwsBedrock;

public sealed class AwsBedrockUsageProviderOptions
{
    public bool Enabled { get; set; }
    public string Region { get; set; } = "us-gov-west-1";
    public string Namespace { get; set; } = "AWS/Bedrock";
    public string[] AllowedProviders { get; set; } = [];
    public string[] AllowedRegions { get; set; } = [];
    public string[] AllowedModels { get; set; } = [];
    public ApprovedModel[] ApprovedModels { get; set; } = [];
    public ModelPrice[] ModelPrices { get; set; } = [];
}


public interface ICloudWatchBedrockClientFactory
{
    ICloudWatchBedrockMetricsClient Create(string region);
}

public interface ICloudWatchBedrockMetricsClient : IDisposable
{
    Task<GetMetricDataResponse> GetMetricDataAsync(GetMetricDataRequest request, CancellationToken cancellationToken);
}

public sealed class CloudWatchBedrockMetricsClient(IAmazonCloudWatch cloudWatch) : ICloudWatchBedrockMetricsClient
{
    public Task<GetMetricDataResponse> GetMetricDataAsync(GetMetricDataRequest request, CancellationToken cancellationToken) => cloudWatch.GetMetricDataAsync(request, cancellationToken);
    public void Dispose() => cloudWatch.Dispose();
}

public sealed class CloudWatchBedrockClientFactory : ICloudWatchBedrockClientFactory
{
    public ICloudWatchBedrockMetricsClient Create(string region) => new CloudWatchBedrockMetricsClient(new AmazonCloudWatchClient(RegionEndpoint.GetBySystemName(region)));
}

public class CloudWatchBedrockUsageProvider(
    IOptions<AwsBedrockUsageProviderOptions> options,
    ICloudWatchBedrockClientFactory cloudWatchClientFactory,
    TokenCostEstimator costEstimator,
    ILogger<CloudWatchBedrockUsageProvider> logger) : IAiUsageProvider
{
    public static readonly string[] MetricNames = ["InputTokenCount", "OutputTokenCount", "Invocations", "CacheReadInputTokenCount", "CachedInputTokenCount", "InputTokenCountFromCache"];
    private static readonly string[] CacheMetricNames = ["CacheReadInputTokenCount", "CachedInputTokenCount", "InputTokenCountFromCache"];

    public string ProviderName => "aws-bedrock";

    public async Task<IReadOnlyList<AiUsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var providerOptions = options.Value;
        if (!providerOptions.Enabled)
        {
            return [];
        }

        var approvedModels = GetApprovedModels(providerOptions).ToArray();
        logger.LogInformation("CloudWatch Bedrock polling active in region {Region} with namespace {Namespace}, {ApprovedModelCount} approved models, and metrics {MetricNames}.", providerOptions.Region, providerOptions.Namespace, approvedModels.Length, string.Join(",", MetricNames));
        if (approvedModels.Length == 0)
        {
            logger.LogWarning("{ProviderName} provider is enabled but has no approved Bedrock models to query.", ProviderName);
            return [];
        }

        var records = new List<AiUsageRecord>();
        foreach (var regionGroup in approvedModels.GroupBy(model => string.IsNullOrWhiteSpace(providerOptions.Region) ? model.Region : providerOptions.Region, StringComparer.OrdinalIgnoreCase))
        {
            using var cloudWatch = cloudWatchClientFactory.Create(regionGroup.Key);
            foreach (var model in regionGroup)
            {
                var record = await GetModelUsageAsync(cloudWatch, model, providerOptions.Namespace, providerOptions.ModelPrices, from, to, cancellationToken);
                if (record is not null)
                {
                    records.Add(record);
                }
            }
        }

        return records;
    }

    private async Task<AiUsageRecord?> GetModelUsageAsync(
        ICloudWatchBedrockMetricsClient cloudWatch,
        ApprovedModel model,
        string metricNamespace,
        IReadOnlyCollection<ModelPrice> prices,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var metricValues = await GetMetricValuesAsync(cloudWatch, model.ModelId, metricNamespace, from, to, cancellationToken);
        var inputTokens = metricValues.GetValueOrDefault("InputTokenCount");
        var outputTokens = metricValues.GetValueOrDefault("OutputTokenCount");
        var requests = metricValues.GetValueOrDefault("Invocations");
        var cachedInputTokens = CacheMetricNames.Sum(metricName => metricValues.GetValueOrDefault(metricName));

        if (inputTokens == 0 && outputTokens == 0 && requests == 0 && cachedInputTokens == 0)
        {
            return null;
        }

        var price = prices.FirstOrDefault(price => string.Equals(price.Provider, ProviderName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(price.ModelId, model.ModelId, StringComparison.OrdinalIgnoreCase))
            ?? new ModelPrice(ProviderName, model.ModelId, 0m, 0m, 0m);
        var cost = costEstimator.Estimate(inputTokens, outputTokens, cachedInputTokens, price);

        return new AiUsageRecord(
            ProviderName,
            model.Region,
            model.ModelId,
            model.Alias,
            from,
            to,
            inputTokens,
            outputTokens,
            cachedInputTokens,
            checked((int)Math.Min(requests, int.MaxValue)),
            cost);
    }

    private static async Task<IReadOnlyDictionary<string, long>> GetMetricValuesAsync(
        ICloudWatchBedrockMetricsClient cloudWatch,
        string modelId,
        string metricNamespace,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var metricNames = MetricNames;
        var request = new GetMetricDataRequest
        {
            StartTime = from.UtcDateTime,
            EndTime = to.UtcDateTime,
            ScanBy = ScanBy.TimestampAscending,
            MetricDataQueries = metricNames.Select((metricName, index) => new MetricDataQuery
            {
                Id = $"m{index}",
                ReturnData = true,
                MetricStat = new MetricStat
                {
                    Period = Math.Max(60, (int)Math.Ceiling((to - from).TotalSeconds)),
                    Stat = "Sum",
                    Metric = new Metric
                    {
                        Namespace = metricNamespace,
                        MetricName = metricName,
                        Dimensions = [new Dimension { Name = "ModelId", Value = modelId }]
                    }
                }
            }).ToList()
        };

        var metricNamesByQueryId = request.MetricDataQueries.ToDictionary(query => query.Id, query => query.MetricStat.Metric.MetricName, StringComparer.OrdinalIgnoreCase);
        var response = await cloudWatch.GetMetricDataAsync(request, cancellationToken);
        return response.MetricDataResults
            .Where(result => metricNamesByQueryId.ContainsKey(result.Id))
            .Select(result => new { MetricName = metricNamesByQueryId[result.Id], Value = result.Values.Sum() })
            .ToDictionary(x => x.MetricName, x => checked((long)Math.Round(x.Value, MidpointRounding.AwayFromZero)), StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<ApprovedModel> GetApprovedModels(AwsBedrockUsageProviderOptions options)
    {
        var allowedModels = options.AllowedModels.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowedRegions = options.AllowedRegions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (allowedRegions.Count == 0 && !string.IsNullOrWhiteSpace(options.Region))
        {
            allowedRegions.Add(options.Region);
        }
        return options.ApprovedModels.Where(model => model.IsApproved
            && string.Equals(model.Provider, "aws-bedrock", StringComparison.OrdinalIgnoreCase)
            && (allowedModels.Count == 0 || allowedModels.Contains(model.ModelId))
            && (allowedRegions.Count == 0 || allowedRegions.Contains(model.Region)));
    }
}

public sealed class AwsBedrockUsageProvider(
    IOptions<AwsBedrockUsageProviderOptions> options,
    ICloudWatchBedrockClientFactory cloudWatchClientFactory,
    TokenCostEstimator costEstimator,
    ILogger<CloudWatchBedrockUsageProvider> logger)
    : CloudWatchBedrockUsageProvider(options, cloudWatchClientFactory, costEstimator, logger);
