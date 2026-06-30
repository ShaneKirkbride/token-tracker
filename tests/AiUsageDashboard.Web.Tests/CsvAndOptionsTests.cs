using AiUsageDashboard.Contracts;
using AiUsageDashboard.Providers.Mock;
using AiUsageDashboard.Web.Configuration;
using AiUsageDashboard.Web.Services;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using AiUsageDashboard.Providers.AwsBedrock;
using AiUsageDashboard.Providers.Gateway;
using AiUsageDashboard.Providers.AzureOpenAI;
using AiUsageDashboard.Providers.GoogleVertex;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiUsageDashboard.Web.Tests;

public sealed class CsvAndOptionsTests
{
    [Fact]
    public void ExportUsage_EscapesCsvValues()
    {
        var service = new CsvExportService();
        var csv = service.ExportUsage([
            new AiUsageRecord("aws", "us-gov-west-1", "model,1", "Model \"One\"", DateTimeOffset.Parse("2026-06-01T00:00:00Z"), DateTimeOffset.Parse("2026-06-01T01:00:00Z"), 1, 2, 3, 4, 5.25m)
        ]);

        Assert.Contains("\"model,1\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Model \"\"One\"\"\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderOptionsValidator_RequiresGatewayBaseUrlWhenEnabled()
    {
        var result = new ProviderOptionsValidator().Validate(null, new ProviderOptions { Gateway = new ProviderEndpointOptions { Enabled = true } });
        Assert.True(result.Failed);
    }

    [Fact]
    public void ModelPricingOptionsValidator_RejectsNegativePrices()
    {
        var result = new ModelPricingOptionsValidator().Validate(null, new ModelPricingOptions { Prices = [new ModelPriceOption { Provider = "p", ModelId = "m", InputPer1MTokensUsd = -1 }] });
        Assert.True(result.Failed);
    }

    [Fact]
    public void ApprovedModelsOptionsValidator_RejectsMissingKeys()
    {
        var result = new ApprovedModelsOptionsValidator().Validate(null, new ApprovedModelsOptions { Models = [new ApprovedModelOption { Provider = "p" }] });
        Assert.True(result.Failed);
    }
    [Fact]
    public void Validators_AcceptValidOptions()
    {
        Assert.False(new ProviderOptionsValidator().Validate(null, new ProviderOptions()).Failed);
        Assert.False(new ApprovedModelsOptionsValidator().Validate(null, new ApprovedModelsOptions { Models = [new ApprovedModelOption { Provider = "p", Region = "r", ModelId = "m", Alias = "a" }] }).Failed);
        Assert.False(new ModelPricingOptionsValidator().Validate(null, new ModelPricingOptions { Prices = [new ModelPriceOption { Provider = "p", ModelId = "m", InputPer1MTokensUsd = 1, OutputPer1MTokensUsd = 2 }] }).Failed);
    }

    [Fact]
    public void ExportUsage_WritesHeaderForNoRows()
    {
        var csv = new CsvExportService().ExportUsage([]);
        Assert.StartsWith("Provider,Region,ModelId", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void UsdCurrencyFormatter_FormatsUsdWithoutGenericCurrencySymbol()
    {
        var formatted = UsdCurrencyFormatter.Format(78.12m);

        Assert.Equal("$78.12", formatted);
        Assert.DoesNotContain("¤", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void UsdCurrencyFormatter_ShowsTinyNonzeroCostsAsLessThanOneCent()
    {
        Assert.Equal("<$0.01", UsdCurrencyFormatter.Format(0.004m));
        Assert.Equal("$0.00", UsdCurrencyFormatter.Format(0m));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void AddMockUsageProviderIfEnabled_RegistersMockOnlyWhenExplicitlyEnabled(bool enabled, int expectedCount)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"{ProviderOptions.SectionName}:Mock:Enabled"] = enabled.ToString() })
            .Build();
        var services = new ServiceCollection();

        services.AddMockUsageProviderIfEnabled(configuration);

        Assert.Equal(expectedCount, services.Count(descriptor => descriptor.ServiceType == typeof(IAiUsageProvider) && descriptor.ImplementationType == typeof(MockUsageProvider)));
    }

    [Fact]
    public void PollingOptions_AcceptsSevenDayLookback()
    {
        var options = new PollingOptions { LookbackMinutes = 10080 };

        var results = Validate(options);

        Assert.Empty(results);
    }

    [Fact]
    public void PollingOptions_RejectsLookbackOverSevenDays()
    {
        var options = new PollingOptions { LookbackMinutes = 10081 };

        var results = Validate(options);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void AddConfiguredUsageProviders_RegistersCloudWatchBedrockFromPrimarySectionOnly()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ProviderOptions.SectionName}:CloudWatchBedrock:Enabled"] = "true",
                [$"{ProviderOptions.SectionName}:CloudWatchBedrock:Region"] = "us-gov-west-1",
                [$"{ProviderOptions.SectionName}:Gateway:Enabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddConfiguredUsageProviders(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAiUsageProvider) && descriptor.ImplementationType == typeof(CloudWatchBedrockUsageProvider));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IAiUsageProvider) && descriptor.ImplementationType == typeof(GatewayUsageProvider));
    }

    [Fact]
    public void AddConfiguredUsageProviders_DoesNotRegisterGatewayWhenDisabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ProviderOptions.SectionName}:Gateway:Enabled"] = "false",
                [$"{ProviderOptions.SectionName}:CloudWatchBedrock:Enabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddConfiguredUsageProviders(configuration);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IAiUsageProvider) && descriptor.ImplementationType == typeof(GatewayUsageProvider));
    }


    [Fact]
    public void AddConfiguredUsageProviders_RegistersExplicitlyEnabledNonBedrockProviders()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ProviderOptions.SectionName}:Gateway:Enabled"] = "true",
                [$"{ProviderOptions.SectionName}:AzureOpenAi:Enabled"] = "true",
                [$"{ProviderOptions.SectionName}:GoogleVertex:Enabled"] = "true",
                [$"{ProviderOptions.SectionName}:CloudWatchBedrock:Enabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddConfiguredUsageProviders(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAiUsageProvider) && descriptor.ImplementationFactory is not null);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAiUsageProvider) && descriptor.ImplementationType == typeof(AzureOpenAiUsageProvider));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAiUsageProvider) && descriptor.ImplementationType == typeof(GoogleVertexUsageProvider));
    }

    private static IReadOnlyList<ValidationResult> Validate(object options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

}
