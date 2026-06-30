using AiUsageDashboard.Contracts;
using AiUsageDashboard.Core;
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
        var service = new DashboardQueryService(repository, Policy("aws", "region", "model a"));

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
        var service = new DashboardQueryService(repository, Policy("aws", "region", "model a"));

        var data = await service.GetDashboardAsync(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), DateTimeOffset.Parse("2026-06-02T00:00:00Z"), CancellationToken.None);

        Assert.Single(data.Records);
        Assert.Equal("aws", data.Records[0].Provider);
        Assert.Single(data.CostOverTime);
        Assert.Equal(1m, data.Summary.EstimatedCostUsd);
        Assert.Equal(1m, data.CostOverTime[0].CostUsd);
        Assert.DoesNotContain("unknown", data.Summary.CostByProvider.Keys);
    }

    [Fact]
    public async Task GetDashboardAsync_FiltersAndAggregatesApprovedSnapshots()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync(CancellationToken.None);
        var repository = new EfUsageSnapshotRepository(db);
        await repository.StoreAsync([
            Record("aws-bedrock", "Jarvis Chat", DateTimeOffset.Parse("2026-06-01T00:00:00Z"), 1m, "openai.gpt-oss-120b-1:0"),
            Record("aws-bedrock", "Jarvis Chat", DateTimeOffset.Parse("2026-06-01T00:15:00Z"), 2m, "openai.gpt-oss-120b-1:0"),
            Record("aws-bedrock", "Unapproved", DateTimeOffset.Parse("2026-06-01T00:00:00Z"), 5m, "unapproved"),
            Record("azure-openai", "Azure", DateTimeOffset.Parse("2026-06-01T00:00:00Z"), 7m, "deployment")
        ], DateTimeOffset.UtcNow, CancellationToken.None);
        var service = new DashboardQueryService(repository, Policy("aws-bedrock", "region", "openai.gpt-oss-120b-1:0"));

        var data = await service.GetDashboardAsync(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), DateTimeOffset.Parse("2026-06-02T00:00:00Z"), CancellationToken.None);

        var row = Assert.Single(data.Records);
        Assert.Equal("Jarvis Chat", row.ModelAlias);
        Assert.Equal("openai.gpt-oss-120b-1:0", row.ModelId);
        Assert.Equal(3m, data.Summary.CostByModel["openai.gpt-oss-120b-1:0"]);
        Assert.DoesNotContain("Jarvis Chat", data.Summary.CostByModel.Keys);
        Assert.Equal(20, row.InputTokens);
        Assert.Equal(10, row.OutputTokens);
        Assert.Equal(6, row.CachedInputTokens);
        Assert.Equal(2, row.Requests);
        Assert.Equal(3m, row.EstimatedCostUsd);
        Assert.Equal(3m, data.Summary.EstimatedCostUsd);
        Assert.Equal(2, data.Summary.TotalRequests);
        Assert.DoesNotContain("azure-openai", data.Summary.CostByProvider.Keys);
    }



    [Fact]
    public async Task GetDashboardAsync_GroupsModelSummaryByModelIdWhenAliasesExist()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync(CancellationToken.None);
        var repository = new EfUsageSnapshotRepository(db);
        await repository.StoreAsync([
            Record("aws-bedrock", "Jarvis Chat", DateTimeOffset.Parse("2026-06-01T00:00:00Z"), 0.07m, "openai.gpt-oss-120b-1:0"),
            Record("aws-bedrock", "Jarvis Chat", DateTimeOffset.Parse("2026-06-01T01:00:00Z"), 0.03m, "openai.gpt-oss-120b-1:0"),
            Record("aws-bedrock", "Llama 3 70B", DateTimeOffset.Parse("2026-06-01T00:00:00Z"), 0.01m, "meta.llama3-70b-instruct-v1:0")
        ], DateTimeOffset.UtcNow, CancellationToken.None);
        var service = new DashboardQueryService(repository, new ApprovedModelPolicy([
            new ApprovedModel("aws-bedrock", "region", "openai.gpt-oss-120b-1:0", "Jarvis Chat", true, true, "Jarvis1"),
            new ApprovedModel("aws-bedrock", "region", "meta.llama3-70b-instruct-v1:0", "Llama 3 70B", true, true, "Jarvis1")
        ]));

        var data = await service.GetDashboardAsync(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), DateTimeOffset.Parse("2026-06-02T00:00:00Z"), CancellationToken.None);

        Assert.Equal(0.11m, data.Summary.EstimatedCostUsd);
        Assert.Equal(["meta.llama3-70b-instruct-v1:0", "openai.gpt-oss-120b-1:0"], data.Summary.CostByModel.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(["meta.llama3-70b-instruct-v1:0", "openai.gpt-oss-120b-1:0"], data.Records.Select(record => record.ModelId).Order(StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain("Jarvis Chat", data.Summary.CostByModel.Keys);
        Assert.DoesNotContain("Llama 3 70B", data.Summary.CostByModel.Keys);
    }

    [Fact]
    public async Task StoreAsync_ReplacesDuplicatePollingSnapshotsForSameWindow()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync(CancellationToken.None);
        var repository = new EfUsageSnapshotRepository(db);
        var start = DateTimeOffset.Parse("2026-06-01T00:00:00Z");

        await repository.StoreAsync([Record("aws-bedrock", "Jarvis Chat", start, 1m, "openai.gpt-oss-120b-1:0")], DateTimeOffset.Parse("2026-06-01T01:00:00Z"), CancellationToken.None);
        await repository.StoreAsync([Record("aws-bedrock", "Jarvis Chat", start, 2m, "openai.gpt-oss-120b-1:0")], DateTimeOffset.Parse("2026-06-01T01:15:00Z"), CancellationToken.None);

        var service = new DashboardQueryService(repository, Policy("aws-bedrock", "region", "openai.gpt-oss-120b-1:0"));
        var data = await service.GetDashboardAsync(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), DateTimeOffset.Parse("2026-06-02T00:00:00Z"), CancellationToken.None);

        var row = Assert.Single(data.Records);
        Assert.Equal(2m, row.EstimatedCostUsd);
        Assert.Equal(1, row.Requests);
    }

    public void Dispose() => _connection.Dispose();


    private static ApprovedModelPolicy Policy(string provider, string region, string modelId) => new([new ApprovedModel(provider, region, modelId, "Model A", true, true, "Jarvis1")]);

    private UsageDashboardDbContext CreateDbContext() => new(new DbContextOptionsBuilder<UsageDashboardDbContext>().UseSqlite(_connection).Options);

    private static AiUsageRecord Record(string provider, string alias, DateTimeOffset start, decimal cost = 1m, string? modelId = null) =>
        new(provider, "region", modelId ?? alias.ToLowerInvariant(), alias, start, start.AddHours(1), 10, 5, 3, 1, cost);

    private sealed class StubProvider(string providerName) : IAiUsageProvider
    {
        public string ProviderName { get; } = providerName;

        public Task<IReadOnlyList<AiUsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AiUsageRecord>>([]);
    }
}
