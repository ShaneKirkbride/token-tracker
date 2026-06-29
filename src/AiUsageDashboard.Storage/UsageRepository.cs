using AiUsageDashboard.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AiUsageDashboard.Storage;

public interface IUsageSnapshotRepository
{
    Task StoreAsync(IEnumerable<AiUsageRecord> records, DateTimeOffset capturedAt, CancellationToken cancellationToken);
    Task<IReadOnlyList<AiUsageRecord>> QueryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
    Task<IReadOnlyList<TimeSeriesPoint>> GetCostOverTimeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelQuota>> GetQuotasAsync(CancellationToken cancellationToken);
}

public sealed class EfUsageSnapshotRepository(UsageDashboardDbContext dbContext) : IUsageSnapshotRepository
{
    public async Task StoreAsync(IEnumerable<AiUsageRecord> records, DateTimeOffset capturedAt, CancellationToken cancellationToken)
    {
        var snapshots = records.Select(record => UsageSnapshot.FromRecord(record, capturedAt)).ToArray();
        if (snapshots.Length == 0)
        {
            return;
        }

        await dbContext.UsageSnapshots.AddRangeAsync(snapshots, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiUsageRecord>> QueryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var rows = await dbContext.UsageSnapshots.Include(x => x.Metrics).AsNoTracking().ToArrayAsync(cancellationToken);
        return rows
            .Where(x => x.WindowStart >= from && x.WindowEnd <= to)
            .OrderBy(x => x.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ModelAlias, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.ToRecord())
            .ToArray();
    }

    public async Task<IReadOnlyList<ModelQuota>> GetQuotasAsync(CancellationToken cancellationToken) =>
        await dbContext.ModelQuotas.AsNoTracking().Select(x => x.ToQuota()).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<TimeSeriesPoint>> GetCostOverTimeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var rows = (await dbContext.UsageSnapshots.Include(x => x.Metrics).AsNoTracking().ToArrayAsync(cancellationToken))
            .Where(x => x.WindowStart >= from && x.WindowEnd <= to)
            .ToArray();

        return rows.GroupBy(x => DateOnly.FromDateTime(x.WindowStart.UtcDateTime.Date))
            .OrderBy(x => x.Key)
            .Select(x => new TimeSeriesPoint(
                x.Key,
                x.Sum(r => r.EstimatedCostUsd ?? 0m),
                x.Sum(r => r.InputTokens + r.OutputTokens + r.CachedInputTokens),
                x.Sum(r => r.Requests),
                x.SelectMany(r => r.Metrics).GroupBy(m => m.Kind).ToDictionary(m => m.Key, m => m.Sum(v => v.Quantity))))
            .ToArray();
    }
}

public interface IDashboardQueryService
{
    Task<DashboardData> GetDashboardAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}

public sealed class DashboardQueryService(IUsageSnapshotRepository repository) : IDashboardQueryService
{
    public async Task<DashboardData> GetDashboardAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var records = await repository.QueryAsync(from, to, cancellationToken);
        var series = await repository.GetCostOverTimeAsync(from, to, cancellationToken);
        var summary = BuildSummary(records);
        var quotas = await repository.GetQuotasAsync(cancellationToken);
        return new DashboardData(summary, records, series, quotas);
    }

    public static DashboardSummary BuildSummary(IEnumerable<AiUsageRecord> records)
    {
        var rows = records.ToArray();
        return new DashboardSummary(
            rows.Sum(x => x.EstimatedCostUsd ?? 0m),
            rows.Sum(x => x.InputTokens + x.OutputTokens + x.CachedInputTokens),
            rows.Sum(x => x.Requests),
            rows.GroupBy(x => x.Provider).ToDictionary(x => x.Key, x => x.Sum(r => r.EstimatedCostUsd ?? 0m)),
            rows.GroupBy(x => x.ModelAlias).ToDictionary(x => x.Key, x => x.Sum(r => r.EstimatedCostUsd ?? 0m)),
            rows.SelectMany(x => x.Metrics).GroupBy(x => x.Kind).ToDictionary(x => x.Key, x => x.Sum(m => m.Quantity)),
            new Dictionary<string, decimal>());
    }
}
