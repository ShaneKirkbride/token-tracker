namespace AiUsageDashboard.Contracts;

public sealed record AiUsageRecord(
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
    decimal EstimatedCostUsd);

public sealed record ModelPrice(
    string Provider,
    string ModelId,
    decimal InputPer1MTokensUsd,
    decimal OutputPer1MTokensUsd,
    decimal CachedInputPer1MTokensUsd = 0m);

public sealed record ApprovedModel(
    string Provider,
    string Region,
    string ModelId,
    string Alias,
    bool IsApproved,
    bool IsGovCloud,
    string EnvironmentTag);

public sealed record UsageQuery(DateTimeOffset From, DateTimeOffset To);

public sealed record TimeSeriesPoint(DateOnly Date, decimal CostUsd, long Tokens, int Requests);

public sealed record DashboardData(
    DashboardSummary Summary,
    IReadOnlyList<AiUsageRecord> Records,
    IReadOnlyList<TimeSeriesPoint> CostOverTime);

public sealed record DashboardSummary(
    decimal EstimatedCostUsd,
    long TotalTokens,
    int TotalRequests,
    IReadOnlyDictionary<string, decimal> CostByProvider,
    IReadOnlyDictionary<string, decimal> CostByModel);

public interface IAiUsageProvider
{
    string ProviderName { get; }

    Task<IReadOnlyList<AiUsageRecord>> GetUsageAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}
