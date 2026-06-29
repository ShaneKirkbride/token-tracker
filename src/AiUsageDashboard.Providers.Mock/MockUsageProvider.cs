using AiUsageDashboard.Contracts;
using AiUsageDashboard.Core;

namespace AiUsageDashboard.Providers.Mock;

public sealed class MockUsageProvider(TokenCostEstimator estimator) : IAiUsageProvider
{
    public string ProviderName => "mock-govcloud";

    public Task<IReadOnlyList<AiUsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prices = new[]
        {
            new ModelPrice("aws-bedrock", "openai.gpt-oss-120b-1:0", 0.15m, 0.60m, 0.03m),
            new ModelPrice("aws-bedrock", "meta.llama3-70b-instruct-v1:0", 0.99m, 0.99m),
            new ModelPrice("azure-openai", "jarvis2-fast", 0.40m, 1.60m, 0.10m),
            new ModelPrice("google-vertex", "gemini-approved", 0.35m, 1.05m)
        };

        IReadOnlyList<AiUsageRecord> records = prices.Select((price, i) =>
        {
            var input = 1_250_000L + (i * 415_000L);
            var output = 480_000L + (i * 215_000L);
            var cached = i % 2 == 0 ? 125_000L : 0L;
            var requests = 300 + (i * 75);
            var metrics = AiUsageRecord.TokenMetrics(input, output, cached, requests);
            return new AiUsageRecord(
                price.Provider,
                price.Provider == "aws-bedrock" ? "us-gov-west-1" : price.Provider == "azure-openai" ? "usgovarizona" : "us-central1",
                price.ModelId,
                price.ModelId.Contains("gpt-oss", StringComparison.OrdinalIgnoreCase) ? "Jarvis Chat" : price.ModelId,
                from,
                to,
                metrics,
                estimator.Estimate(metrics, price.ToMeterPrices()));
        }).Append(new AiUsageRecord("aws-bedrock", "us-gov-west-1", "batch-import", "Batch import", from, to, [new UsageMetric(UsageMeterKind.BatchInputGigabytes, 12.5m, "GB", "Batch input"), new UsageMetric(UsageMeterKind.Unknown, 3, "events", "Vendor-specific meter")], null)).ToArray();

        return Task.FromResult(records);
    }
}
