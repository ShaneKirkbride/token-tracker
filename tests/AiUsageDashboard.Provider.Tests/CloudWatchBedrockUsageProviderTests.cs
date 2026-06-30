using AiUsageDashboard.Contracts;
using AiUsageDashboard.Core;
using AiUsageDashboard.Providers.AwsBedrock;
using Amazon.CloudWatch.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiUsageDashboard.Provider.Tests;

public sealed class CloudWatchBedrockUsageProviderTests
{
    private static readonly DateTimeOffset From = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
    private static readonly DateTimeOffset To = DateTimeOffset.Parse("2026-06-01T01:00:00Z");

    [Fact]
    public async Task GetUsageAsync_MapsCloudWatchMetricsToAiUsageRecord()
    {
        var provider = CreateProvider(new Dictionary<string, double[]> { ["InputTokenCount"] = [100], ["OutputTokenCount"] = [50], ["Invocations"] = [3], ["CachedInputTokenCount"] = [20] });

        var records = await provider.GetUsageAsync(From, To, CancellationToken.None);

        var record = Assert.Single(records);
        Assert.Equal("aws-bedrock", record.Provider);
        Assert.Equal("us-gov-west-1", record.Region);
        Assert.Equal("openai.gpt-oss-120b-1:0", record.ModelId);
        Assert.Equal("Jarvis Chat", record.ModelAlias);
        Assert.Equal(100, record.InputTokens);
        Assert.Equal(50, record.OutputTokens);
        Assert.Equal(20, record.CachedInputTokens);
        Assert.Equal(3, record.Requests);
    }

    [Fact]
    public async Task GetUsageAsync_TreatsMissingMetricsAsZeroAndSkipsEmptyModels()
    {
        var provider = CreateProvider(new Dictionary<string, double[]> { ["Invocations"] = [2] });

        var records = await provider.GetUsageAsync(From, To, CancellationToken.None);

        var record = Assert.Single(records);
        Assert.Equal(0, record.InputTokens);
        Assert.Equal(0, record.OutputTokens);
        Assert.Equal(2, record.Requests);
    }

    [Fact]
    public async Task GetUsageAsync_FiltersToApprovedAllowlistedJarvisModels()
    {
        var options = CreateOptions(["openai.gpt-oss-120b-1:0"]);
        options.ApprovedModels = [.. options.ApprovedModels, new ApprovedModel("aws-bedrock", "us-gov-west-1", "anthropic.claude-3-5-sonnet", "Not Jarvis", true, true, "Jarvis1")];
        var provider = new CloudWatchBedrockUsageProvider(Options.Create(options), new FakeFactory(new Dictionary<string, double[]> { ["InputTokenCount"] = [10] }), new TokenCostEstimator(), NullLogger<CloudWatchBedrockUsageProvider>.Instance);

        var records = await provider.GetUsageAsync(From, To, CancellationToken.None);

        var record = Assert.Single(records);
        Assert.Equal("openai.gpt-oss-120b-1:0", record.ModelId);
    }

    [Fact]
    public async Task GetUsageAsync_CalculatesCostUsingConfiguredPricing()
    {
        var provider = CreateProvider(new Dictionary<string, double[]> { ["InputTokenCount"] = [1_000_000], ["OutputTokenCount"] = [500_000], ["Invocations"] = [1], ["CachedInputTokenCount"] = [100_000] });

        var records = await provider.GetUsageAsync(From, To, CancellationToken.None);

        var record = Assert.Single(records);
        Assert.Equal(0.438m, record.EstimatedCostUsd);
    }

    private static CloudWatchBedrockUsageProvider CreateProvider(Dictionary<string, double[]> metrics)
        => new(Options.Create(CreateOptions(["openai.gpt-oss-120b-1:0"])), new FakeFactory(metrics), new TokenCostEstimator(), NullLogger<CloudWatchBedrockUsageProvider>.Instance);

    private static AwsBedrockUsageProviderOptions CreateOptions(string[] allowedModels) => new()
    {
        Enabled = true,
        AllowedRegions = ["us-gov-west-1"],
        AllowedModels = allowedModels,
        ApprovedModels =
        [
            new ApprovedModel("aws-bedrock", "us-gov-west-1", "openai.gpt-oss-120b-1:0", "Jarvis Chat", true, true, "Jarvis1"),
            new ApprovedModel("aws-bedrock", "us-gov-west-1", "meta.llama3-70b-instruct-v1:0", "Llama 3 70B", true, true, "Jarvis1")
        ],
        ModelPrices = [new ModelPrice("aws-bedrock", "openai.gpt-oss-120b-1:0", 0.15m, 0.6m, 0.03m)]
    };

    private sealed class FakeFactory(Dictionary<string, double[]> metrics) : ICloudWatchBedrockClientFactory
    {
        public ICloudWatchBedrockMetricsClient Create(string region) => new FakeClient(metrics);
    }

    private sealed class FakeClient(Dictionary<string, double[]> metrics) : ICloudWatchBedrockMetricsClient
    {
        public Task<GetMetricDataResponse> GetMetricDataAsync(GetMetricDataRequest request, CancellationToken cancellationToken)
        {
            var results = request.MetricDataQueries.Select(query =>
            {
                var values = metrics.GetValueOrDefault(query.MetricStat.Metric.MetricName, []);
                return new MetricDataResult { Id = query.Id, Values = values.ToList() };
            }).ToList();
            return Task.FromResult(new GetMetricDataResponse { MetricDataResults = results });
        }

        public void Dispose()
        {
        }
    }
}
