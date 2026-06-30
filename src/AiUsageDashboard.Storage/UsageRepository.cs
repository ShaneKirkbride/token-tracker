using AiUsageDashboard.Contracts;
using AiUsageDashboard.Core;
using Microsoft.EntityFrameworkCore;

namespace AiUsageDashboard.Storage;

public interface IUsageSnapshotRepository
{
    Task StoreAsync(IEnumerable<AiUsageRecord> records, DateTimeOffset capturedAt, CancellationToken cancellationToken);
    Task<IReadOnlyList<AiUsageRecord>> QueryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
    Task<IReadOnlyList<TimeSeriesPoint>> GetCostOverTimeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
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
        var rows = await dbContext.UsageSnapshots.AsNoTracking().ToArrayAsync(cancellationToken);
        return rows
            .Where(x => x.WindowStart >= from && x.WindowEnd <= to)
            .OrderBy(x => x.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ModelAlias, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.ToRecord())
            .ToArray();
    }

    public async Task<IReadOnlyList<TimeSeriesPoint>> GetCostOverTimeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var rows = (await dbContext.UsageSnapshots.AsNoTracking().ToArrayAsync(cancellationToken))
            .Where(x => x.WindowStart >= from && x.WindowEnd <= to)
            .ToArray();

        return rows.GroupBy(x => DateOnly.FromDateTime(x.WindowStart.UtcDateTime.Date))
            .OrderBy(x => x.Key)
            .Select(x => new TimeSeriesPoint(
                x.Key,
                x.Sum(r => r.EstimatedCostUsd),
                x.Sum(r => r.InputTokens + r.OutputTokens + r.CachedInputTokens),
                x.Sum(r => r.Requests)))
            .ToArray();
    }
}

public interface IDashboardQueryService
{
    Task<DashboardData> GetDashboardAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}

public sealed class DashboardQueryService(IUsageSnapshotRepository repository, ApprovedModelPolicy approvedModelPolicy) : IDashboardQueryService
{
    public async Task<DashboardData> GetDashboardAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var records = (await repository.QueryAsync(from, to, cancellationToken))
            .Where(record => approvedModelPolicy.IsApproved(record.Provider, record.Region, record.ModelId))
            .GroupBy(record => new { record.Provider, record.Region, record.ModelId, record.ModelAlias })
            .Select(group => new AiUsageRecord(
                group.Key.Provider,
                group.Key.Region,
                group.Key.ModelId,
                group.Key.ModelAlias,
                group.Min(record => record.WindowStart),
                group.Max(record => record.WindowEnd),
                group.Sum(record => record.InputTokens),
                group.Sum(record => record.OutputTokens),
                group.Sum(record => record.CachedInputTokens),
                group.Sum(record => record.Requests),
                group.Sum(record => record.EstimatedCostUsd)))
            .OrderBy(record => record.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.ModelAlias, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var series = BuildCostOverTime(records);
        var summary = BuildSummary(records);
        return new DashboardData(summary, records, series);
    }

    public static IReadOnlyList<TimeSeriesPoint> BuildCostOverTime(IEnumerable<AiUsageRecord> records)
    {
        return records
            .GroupBy(x => DateOnly.FromDateTime(x.WindowStart.UtcDateTime.Date))
            .OrderBy(x => x.Key)
            .Select(x => new TimeSeriesPoint(
                x.Key,
                x.Sum(r => r.EstimatedCostUsd),
                x.Sum(r => r.InputTokens + r.OutputTokens + r.CachedInputTokens),
                x.Sum(r => r.Requests)))
            .ToArray();
    }

    public static DashboardSummary BuildSummary(IEnumerable<AiUsageRecord> records)
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
