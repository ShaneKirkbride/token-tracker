using AiUsageDashboard.Contracts;
using AiUsageDashboard.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AiUsageDashboard.Storage.Tests;

public sealed class UsageRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public UsageRepositoryTests()
    {
        _connection.Open();
    }

    [Fact]
    public async Task StoreAndQueryAsync_PersistsUsageMetadata()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync(CancellationToken.None);
        var repository = new EfUsageSnapshotRepository(db);
        var record = Record("aws", "Model A", DateTimeOffset.Parse("2026-06-01T00:00:00Z"));

        await repository.StoreAsync([record], DateTimeOffset.Parse("2026-06-01T01:00:00Z"), CancellationToken.None);
        var records = await repository.QueryAsync(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), DateTimeOffset.Parse("2026-06-01T02:00:00Z"), CancellationToken.None);

        Assert.Single(records);
        Assert.Equal("Model A", records[0].ModelAlias);
    }

    [Fact]
    public async Task GetCostOverTimeAsync_GroupsByUtcDate()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync(CancellationToken.None);
        var repository = new EfUsageSnapshotRepository(db);
        await repository.StoreAsync([
            Record("aws", "Model A", DateTimeOffset.Parse("2026-06-01T00:00:00Z"), 1.25m),
            Record("azure", "Model B", DateTimeOffset.Parse("2026-06-01T02:00:00Z"), 2.75m)
        ], DateTimeOffset.UtcNow, CancellationToken.None);

        var points = await repository.GetCostOverTimeAsync(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), DateTimeOffset.Parse("2026-06-02T00:00:00Z"), CancellationToken.None);

        Assert.Single(points);
        Assert.Equal(4m, points[0].CostUsd);
        Assert.Equal(36, points[0].Tokens);
    }

    [Fact]
    public void BuildSummary_GroupsDashboardData()
    {
        var summary = DashboardQueryService.BuildSummary([Record("aws", "Model A", DateTimeOffset.UtcNow, 1m), Record("aws", "Model B", DateTimeOffset.UtcNow, 2m)]);
        Assert.Equal(3m, summary.EstimatedCostUsd);
        Assert.Equal(2, summary.TotalRequests);
        Assert.Equal(3m, summary.CostByProvider["aws"]);
    }


    [Fact]
    public async Task StoreAsync_IgnoresEmptyBatchesAndDashboardQueryReturnsData()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync(CancellationToken.None);
        var repository = new EfUsageSnapshotRepository(db);
        await repository.StoreAsync([], DateTimeOffset.UtcNow, CancellationToken.None);
        await repository.StoreAsync([Record("aws", "Model A", DateTimeOffset.Parse("2026-06-01T00:00:00Z"), 1m)], DateTimeOffset.UtcNow, CancellationToken.None);
        var service = new DashboardQueryService(repository, [new StubProvider("aws")]);

        var data = await service.GetDashboardAsync(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), DateTimeOffset.Parse("2026-06-02T00:00:00Z"), CancellationToken.None);

        Assert.Single(data.Records);
        Assert.Single(data.CostOverTime);
        Assert.Equal(1m, data.Summary.EstimatedCostUsd);
    }

    [Fact]
    public async Task GetDashboardAsync_HidesRecordsForUnknownProviders()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync(CancellationToken.None);
        var repository = new EfUsageSnapshotRepository(db);
        await repository.StoreAsync([
            Record("aws", "Model A", DateTimeOffset.Parse("2026-06-01T00:00:00Z"), 1m),
            Record("unknown", "Model B", DateTimeOffset.Parse("2026-06-01T02:00:00Z"), 2m)
        ], DateTimeOffset.UtcNow, CancellationToken.None);
        var service = new DashboardQueryService(repository, [new StubProvider("aws")]);

        var data = await service.GetDashboardAsync(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), DateTimeOffset.Parse("2026-06-02T00:00:00Z"), CancellationToken.None);

        Assert.Single(data.Records);
        Assert.Equal("aws", data.Records[0].Provider);
        Assert.Single(data.CostOverTime);
        Assert.Equal(1m, data.Summary.EstimatedCostUsd);
        Assert.Equal(1m, data.CostOverTime[0].CostUsd);
        Assert.DoesNotContain("unknown", data.Summary.CostByProvider.Keys);
    }

    public void Dispose() => _connection.Dispose();

    private UsageDashboardDbContext CreateDbContext() => new(new DbContextOptionsBuilder<UsageDashboardDbContext>().UseSqlite(_connection).Options);

    private static AiUsageRecord Record(string provider, string alias, DateTimeOffset start, decimal cost = 1m) =>
        new(provider, "region", alias.ToLowerInvariant(), alias, start, start.AddHours(1), 10, 5, 3, 1, cost);

    private sealed class StubProvider(string providerName) : IAiUsageProvider
    {
        public string ProviderName { get; } = providerName;

        public Task<IReadOnlyList<AiUsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AiUsageRecord>>([]);
    }
}
