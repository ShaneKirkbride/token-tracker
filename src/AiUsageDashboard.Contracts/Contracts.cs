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

public interface IAiUsageProvider
{
    string ProviderName { get; }

    Task<IReadOnlyList<AiUsageRecord>> GetUsageAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}
