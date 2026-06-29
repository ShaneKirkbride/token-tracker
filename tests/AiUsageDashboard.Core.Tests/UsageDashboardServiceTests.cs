using AiUsageDashboard.Contracts;
using AiUsageDashboard.Core;

namespace AiUsageDashboard.Core.Tests;

public sealed class UsageDashboardServiceTests
{
    [Fact]
    public async Task GetUsageAsync_FlattensAndSortsProviderResults()
    {
        var from = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
        var to = from.AddDays(1);
        var sut = new UsageDashboardService([
            new StubProvider("z-provider", [Record("z-provider", "Zulu")]),
            new StubProvider("a-provider", [Record("a-provider", "Alpha")])
        ]);

        var records = await sut.GetUsageAsync(from, to, TestContext.Current.CancellationToken);

        Assert.Collection(records,
            first => Assert.Equal("a-provider", first.Provider),
            second => Assert.Equal("z-provider", second.Provider));
    }

    [Fact]
    public async Task GetUsageAsync_RejectsInvalidWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var sut = new UsageDashboardService([]);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetUsageAsync(now, now, TestContext.Current.CancellationToken));
    }


    [Fact]
    public async Task GetUsageAsync_ContinuesWhenProviderFails()
    {
        var from = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
        var to = from.AddDays(1);
        var sut = new UsageDashboardService([
            new FailingProvider(),
            new StubProvider("a-provider", [Record("a-provider", "Alpha")])
        ]);

        var records = await sut.GetUsageAsync(from, to, TestContext.Current.CancellationToken);

        Assert.Single(records);
        Assert.Equal("a-provider", records[0].Provider);
    }

    [Fact]
    public void Summarize_GroupsCostByProviderAndModel()
    {
        var sut = new UsageDashboardService([]);
        var records = new[]
        {
            Record("aws", "Model A", cost: 1.25m, requests: 2, input: 10, output: 20, cached: 3),
            Record("aws", "Model B", cost: 2.25m, requests: 3, input: 100, output: 200, cached: 30),
            Record("azure", "Model A", cost: 3.50m, requests: 4, input: 1, output: 2, cached: 0)
        };

        var summary = sut.Summarize(records);

        Assert.Equal(7.00m, summary.EstimatedCostUsd);
        Assert.Equal(366, summary.TotalTokens);
        Assert.Equal(9, summary.TotalRequests);
        Assert.Equal(3.50m, summary.CostByProvider["aws"]);
        Assert.Equal(3.50m, summary.CostByProvider["azure"]);
        Assert.Equal(4.75m, summary.CostByModel["Model A"]);
        Assert.Equal(2.25m, summary.CostByModel["Model B"]);
    }

    private static AiUsageRecord Record(string provider, string alias, decimal cost = 0m, int requests = 1, long input = 1, long output = 1, long cached = 0) =>
        new(provider, "region", alias.ToLowerInvariant().Replace(' ', '-'), alias, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, input, output, cached, requests, cost);

    private sealed class FailingProvider : IAiUsageProvider
    {
        public string ProviderName => "failing";
        public Task<IReadOnlyList<AiUsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }

    private sealed class StubProvider(string name, IReadOnlyList<AiUsageRecord> records) : IAiUsageProvider
    {
        public string ProviderName => name;
        public Task<IReadOnlyList<AiUsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) => Task.FromResult(records);
    }
}
