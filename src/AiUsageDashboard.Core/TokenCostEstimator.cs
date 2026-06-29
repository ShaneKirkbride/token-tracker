using AiUsageDashboard.Contracts;

namespace AiUsageDashboard.Core;

public sealed class TokenCostEstimator
{
    public decimal Estimate(long inputTokens, long outputTokens, long cachedInputTokens, ModelPrice price)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(outputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(cachedInputTokens);
        ArgumentNullException.ThrowIfNull(price);

        var billableInputTokens = Math.Max(0, inputTokens - cachedInputTokens);
        var inputCost = billableInputTokens / 1_000_000m * price.InputPer1MTokensUsd;
        var outputCost = outputTokens / 1_000_000m * price.OutputPer1MTokensUsd;
        var cachedCost = cachedInputTokens / 1_000_000m * price.CachedInputPer1MTokensUsd;

        return Math.Round(inputCost + outputCost + cachedCost, 6, MidpointRounding.AwayFromZero);
    }

    public decimal? Estimate(IEnumerable<UsageMetric> metrics, IEnumerable<ModelMeterPrice> prices)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(prices);

        var priceByMeter = prices.ToDictionary(x => x.MeterKind);
        var total = 0m;
        var pricedAny = false;
        foreach (var metric in metrics.Where(x => x.Quantity > 0 && x.Kind != UsageMeterKind.TotalTokens))
        {
            if (!priceByMeter.TryGetValue(metric.Kind, out var price) || price.UnitQuantity <= 0)
            {
                continue;
            }

            total += metric.Quantity / price.UnitQuantity * price.PriceUsd;
            pricedAny = true;
        }

        return pricedAny ? Math.Round(total, 6, MidpointRounding.AwayFromZero) : null;
    }
}
