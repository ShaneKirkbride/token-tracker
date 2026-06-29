namespace AiUsageDashboard.Contracts;

public enum UsageMeterKind
{
    TotalTokens,
    InputTokens,
    OutputTokens,
    CachedInputTokens,
    Requests,
    RequestsPerMinute,
    BatchInputGigabytes,
    Images,
    AudioSeconds,
    VideoSeconds,
    Characters,
    Unknown
}

public sealed record UsageMetric(UsageMeterKind Kind, decimal Quantity, string Unit, string? Name = null)
{
    public bool IsToken => Kind is UsageMeterKind.TotalTokens or UsageMeterKind.InputTokens or UsageMeterKind.OutputTokens or UsageMeterKind.CachedInputTokens;
}

public sealed record AiUsageRecord(
    string Provider,
    string Region,
    string ModelId,
    string ModelAlias,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    IReadOnlyList<UsageMetric> Metrics,
    decimal? EstimatedCostUsd)
{
    public AiUsageRecord(
        string provider,
        string region,
        string modelId,
        string modelAlias,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        long inputTokens,
        long outputTokens,
        long cachedInputTokens,
        int requests,
        decimal estimatedCostUsd)
        : this(provider, region, modelId, modelAlias, windowStart, windowEnd, TokenMetrics(inputTokens, outputTokens, cachedInputTokens, requests), estimatedCostUsd)
    {
    }

    public long InputTokens => WholeQuantity(UsageMeterKind.InputTokens);
    public long OutputTokens => WholeQuantity(UsageMeterKind.OutputTokens);
    public long CachedInputTokens => WholeQuantity(UsageMeterKind.CachedInputTokens);
    public int Requests => (int)WholeQuantity(UsageMeterKind.Requests);
    public long TotalTokens => Metrics.Any(x => x.Kind == UsageMeterKind.TotalTokens)
        ? WholeQuantity(UsageMeterKind.TotalTokens)
        : InputTokens + OutputTokens + CachedInputTokens;
    public bool HasUnpricedMeters => EstimatedCostUsd is null && Metrics.Count > 0;
    public bool HasUnknownMeters => Metrics.Any(x => x.Kind == UsageMeterKind.Unknown);

    public static IReadOnlyList<UsageMetric> TokenMetrics(long inputTokens, long outputTokens, long cachedInputTokens, int requests) =>
    [
        new(UsageMeterKind.InputTokens, inputTokens, "tokens", "Input tokens"),
        new(UsageMeterKind.OutputTokens, outputTokens, "tokens", "Output tokens"),
        new(UsageMeterKind.CachedInputTokens, cachedInputTokens, "tokens", "Cached input tokens"),
        new(UsageMeterKind.TotalTokens, inputTokens + outputTokens + cachedInputTokens, "tokens", "Total tokens"),
        new(UsageMeterKind.Requests, requests, "requests", "Requests")
    ];

    private long WholeQuantity(UsageMeterKind kind) => (long)Metrics.Where(x => x.Kind == kind).Sum(x => x.Quantity);
}

public sealed record ModelPrice(
    string Provider,
    string ModelId,
    decimal InputPer1MTokensUsd,
    decimal OutputPer1MTokensUsd,
    decimal CachedInputPer1MTokensUsd = 0m)
{
    public IReadOnlyList<ModelMeterPrice> ToMeterPrices() =>
    [
        new(Provider, ModelId, UsageMeterKind.InputTokens, InputPer1MTokensUsd, 1_000_000m, "tokens"),
        new(Provider, ModelId, UsageMeterKind.OutputTokens, OutputPer1MTokensUsd, 1_000_000m, "tokens"),
        new(Provider, ModelId, UsageMeterKind.CachedInputTokens, CachedInputPer1MTokensUsd, 1_000_000m, "tokens")
    ];
}

public sealed record ModelMeterPrice(string Provider, string ModelId, UsageMeterKind MeterKind, decimal PriceUsd, decimal UnitQuantity, string Unit);

public sealed record ModelQuota(string Provider, string Region, string ModelId, UsageMeterKind MeterKind, decimal Limit, TimeSpan Window, string QuotaName, string Unit);

public sealed record ApprovedModel(
    string Provider,
    string Region,
    string ModelId,
    string Alias,
    bool IsApproved,
    bool IsGovCloud,
    string EnvironmentTag);

public sealed record UsageQuery(DateTimeOffset From, DateTimeOffset To);

public sealed record TimeSeriesPoint(DateOnly Date, decimal CostUsd, long Tokens, int Requests, IReadOnlyDictionary<UsageMeterKind, decimal>? UsageByMeter = null);

public sealed record DashboardData(
    DashboardSummary Summary,
    IReadOnlyList<AiUsageRecord> Records,
    IReadOnlyList<TimeSeriesPoint> CostOverTime,
    IReadOnlyList<ModelQuota> Quotas);

public sealed record DashboardSummary(
    decimal EstimatedCostUsd,
    long TotalTokens,
    int TotalRequests,
    IReadOnlyDictionary<string, decimal> CostByProvider,
    IReadOnlyDictionary<string, decimal> CostByModel,
    IReadOnlyDictionary<UsageMeterKind, decimal>? UsageByMeter = null,
    IReadOnlyDictionary<string, decimal>? QuotaUtilizationPercent = null);

public interface IAiUsageProvider
{
    string ProviderName { get; }

    Task<IReadOnlyList<AiUsageRecord>> GetUsageAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}
