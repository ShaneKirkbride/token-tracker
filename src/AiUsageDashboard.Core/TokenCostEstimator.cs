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
}
