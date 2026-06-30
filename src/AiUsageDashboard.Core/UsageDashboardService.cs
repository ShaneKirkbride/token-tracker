using AiUsageDashboard.Contracts;
using Microsoft.Extensions.Logging;

namespace AiUsageDashboard.Core;

public sealed class UsageDashboardService(IEnumerable<IAiUsageProvider> providers, ApprovedModelPolicy? approvedModelPolicy = null, ILogger<UsageDashboardService>? logger = null)
{
    private readonly IReadOnlyList<IAiUsageProvider> _providers = providers.ToList();

    public async Task<IReadOnlyList<AiUsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        if (to <= from)
        {
            throw new ArgumentException("The end date must be after the start date.", nameof(to));
        }

        var results = new List<AiUsageRecord>();
        foreach (var provider in _providers)
        {
            try
            {
                var providerRecords = await provider.GetUsageAsync(from, to, cancellationToken);
                results.AddRange(providerRecords);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Usage provider {ProviderName} failed for {From:o} to {To:o}.", provider.ProviderName, from, to);
            }
        }

        var filteredResults = approvedModelPolicy is null ? results : approvedModelPolicy.Filter(results);

        return filteredResults
            .OrderBy(x => x.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ModelAlias, StringComparer.OrdinalIgnoreCase)
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
