using AiUsageDashboard.Contracts;
using AiUsageDashboard.Core;

namespace AiUsageDashboard.Core.Tests;

public sealed class TokenCostEstimatorTests
{
    [Fact]
    public void Estimate_CalculatesInputOutputAndCachedTokenCost()
    {
        var sut = new TokenCostEstimator();
        var price = new ModelPrice("aws-bedrock", "m", 2m, 8m, .5m);

        var cost = sut.Estimate(2_000_000, 500_000, 250_000, price);

        Assert.Equal(7.625m, cost);
    }

    [Fact]
    public void Estimate_RoundsAwayFromZeroToSixDecimals()
    {
        var sut = new TokenCostEstimator();
        var price = new ModelPrice("azure-openai", "m", 0.333333m, 0.777777m);

        var cost = sut.Estimate(1, 1, 0, price);

        Assert.Equal(0.000001m, cost);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    public void Estimate_RejectsNegativeTokens(long input, long output, long cached)
    {
        var sut = new TokenCostEstimator();
        var price = new ModelPrice("p", "m", 1m, 1m);

        Assert.Throws<ArgumentOutOfRangeException>(() => sut.Estimate(input, output, cached, price));
    }
}

public sealed class MeterCostEstimatorTests
{
    [Fact]
    public void Estimate_UsesMeterPricesAndIgnoresUnpricedMeters()
    {
        var sut = new TokenCostEstimator();
        var cost = sut.Estimate(
            [new UsageMetric(UsageMeterKind.Images, 3, "images"), new UsageMetric(UsageMeterKind.Unknown, 9, "events")],
            [new ModelMeterPrice("p", "m", UsageMeterKind.Images, 0.02m, 1, "images")]);

        Assert.Equal(0.06m, cost);
    }

    [Fact]
    public void Estimate_ReturnsNullWhenNoMetersArePriced()
    {
        var sut = new TokenCostEstimator();
        var cost = sut.Estimate([new UsageMetric(UsageMeterKind.AudioSeconds, 30, "seconds")], []);

        Assert.Null(cost);
    }
}
