using AiUsageDashboard.Contracts;

namespace AiUsageDashboard.Providers.AwsBedrock;

public static class AwsBedrockQuotaMapper
{
    public static ModelQuota Map(string provider, string region, string modelId, string quotaName, decimal limit, string unit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(quotaName);

        var normalized = quotaName.ToLowerInvariant();
        if (normalized.Contains("tokens per day", StringComparison.Ordinal) || normalized.Contains("maximum tokens", StringComparison.Ordinal))
        {
            return new ModelQuota(provider, region, modelId, UsageMeterKind.TotalTokens, limit, TimeSpan.FromDays(1), quotaName, string.IsNullOrWhiteSpace(unit) ? "tokens" : unit);
        }

        if (normalized.Contains("requests per minute", StringComparison.Ordinal))
        {
            return new ModelQuota(provider, region, modelId, UsageMeterKind.RequestsPerMinute, limit, TimeSpan.FromMinutes(1), quotaName, string.IsNullOrWhiteSpace(unit) ? "requests/minute" : unit);
        }

        if (normalized.Contains("job size", StringComparison.Ordinal) && normalized.Contains("gb", StringComparison.Ordinal))
        {
            return new ModelQuota(provider, region, modelId, UsageMeterKind.BatchInputGigabytes, limit, TimeSpan.Zero, quotaName, string.IsNullOrWhiteSpace(unit) ? "GB" : unit);
        }

        return new ModelQuota(provider, region, modelId, UsageMeterKind.Unknown, limit, TimeSpan.Zero, quotaName, unit);
    }
}
