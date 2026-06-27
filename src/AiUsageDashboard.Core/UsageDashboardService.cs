using AiUsageDashboard.Contracts;

namespace AiUsageDashboard.Core;

public sealed class UsageDashboardService(IEnumerable<IAiUsageProvider> providers)
{
    private readonly IReadOnlyList<IAiUsageProvider> _providers = providers.ToList();

    public async Task<IReadOnlyList<AiUsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        if (to <= from)
        {
            throw new ArgumentException("The end date must be after the start date.", nameof(to));
        }

        var results = await Task.WhenAll(_providers.Select(p => p.GetUsageAsync(from, to, cancellationToken)));
        return results.SelectMany(x => x)
            .OrderBy(x => x.Provider)
            .ThenBy(x => x.ModelAlias)
            .ToArray();
    }

    public DashboardSummary Summarize(IEnumerable<AiUsageRecord> records)
    {
        var rows = records.ToArray();
        return new DashboardSummary(
            rows.Sum(x => x.EstimatedCostUsd),
            rows.Sum(x => x.InputTokens + x.OutputTokens + x.CachedInputTokens),
            rows.Sum(x => x.Requests),
            rows.GroupBy(x => x.Provider).ToDictionary(x => x.Key, x => x.Sum(r => r.EstimatedCostUsd)),
            rows.GroupBy(x => x.ModelAlias).ToDictionary(x => x.Key, x => x.Sum(r => r.EstimatedCostUsd)));
    }
}

public sealed record DashboardSummary(
    decimal EstimatedCostUsd,
    long TotalTokens,
    int TotalRequests,
    IReadOnlyDictionary<string, decimal> CostByProvider,
    IReadOnlyDictionary<string, decimal> CostByModel);
