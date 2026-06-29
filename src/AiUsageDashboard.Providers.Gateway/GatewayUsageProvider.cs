using System.Net.Http.Json;
using AiUsageDashboard.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiUsageDashboard.Providers.Gateway;

public sealed class GatewayUsageProviderOptions
{
    public Uri? BaseUrl { get; set; }
    public bool Enabled { get; set; }
}

public sealed class GatewayUsageProvider(HttpClient httpClient, IOptions<GatewayUsageProviderOptions> options, ILogger<GatewayUsageProvider> logger) : IAiUsageProvider
{
    public string ProviderName => "gateway";

    public async Task<IReadOnlyList<AiUsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return [];
        }

        if (options.Value.BaseUrl is null)
        {
            throw new InvalidOperationException("Gateway base URL is required when the gateway provider is enabled.");
        }

        var requestUri = new Uri(options.Value.BaseUrl, $"admin/usage?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        try
        {
            var records = await httpClient.GetFromJsonAsync<GatewayUsageRecord[]>(requestUri, cancellationToken);
            return records?.Select(x => x.ToUsageRecord()).ToArray() ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gateway usage request failed for {From:o} to {To:o}.", from, to);
            return [];
        }
    }

    private sealed record GatewayUsageRecord(
        string Provider,
        string Region,
        string ModelId,
        string ModelAlias,
        DateTimeOffset WindowStart,
        DateTimeOffset WindowEnd,
        long InputTokens,
        long OutputTokens,
        long CachedInputTokens,
        int Requests,
        decimal? EstimatedCostUsd,
        UsageMetric[]? Metrics)
    {
        public AiUsageRecord ToUsageRecord()
        {
            var metrics = Metrics is { Length: > 0 }
                ? Metrics
                : AiUsageRecord.TokenMetrics(InputTokens, OutputTokens, CachedInputTokens, Requests);
            return new AiUsageRecord(Provider, Region, ModelId, ModelAlias, WindowStart, WindowEnd, metrics, EstimatedCostUsd);
        }
    }
}
