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

        Assert.Equal(7.125m, cost);
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
