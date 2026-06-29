using Microsoft.Extensions.Options;

namespace AiUsageDashboard.Web.Configuration;

public sealed class ProviderOptionsValidator : IValidateOptions<ProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, ProviderOptions options)
    {
        if (options.Gateway.Enabled && options.Gateway.BaseUrl is null)
        {
            return ValidateOptionsResult.Fail("Gateway provider requires Providers:Gateway:BaseUrl when enabled.");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class ApprovedModelsOptionsValidator : IValidateOptions<ApprovedModelsOptions>
{
    public ValidateOptionsResult Validate(string? name, ApprovedModelsOptions options)
    {
        var invalid = options.Models.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.Provider) || string.IsNullOrWhiteSpace(x.Region) || string.IsNullOrWhiteSpace(x.ModelId));
        return invalid is null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("Approved models require provider, region, and model id.");
    }
}

public sealed class ModelPricingOptionsValidator : IValidateOptions<ModelPricingOptions>
{
    public ValidateOptionsResult Validate(string? name, ModelPricingOptions options)
    {
        var invalid = options.Prices.FirstOrDefault(x => x.InputPer1MTokensUsd < 0 || x.OutputPer1MTokensUsd < 0 || x.CachedInputPer1MTokensUsd < 0);
        return invalid is null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("Model prices cannot be negative.");
    }
}
