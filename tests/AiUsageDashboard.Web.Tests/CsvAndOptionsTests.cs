using AiUsageDashboard.Contracts;
using AiUsageDashboard.Web.Configuration;
using AiUsageDashboard.Web.Services;
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

}

public sealed class MeterCsvExportTests
{
    [Fact]
    public void ExportUsage_WritesOneRowPerMeterWithPricingBadges()
    {
        var csv = new CsvExportService().ExportUsage([
            new AiUsageRecord("p", "r", "m", "alias", DateTimeOffset.Parse("2026-06-01T00:00:00Z"), DateTimeOffset.Parse("2026-06-01T01:00:00Z"),
                [new UsageMetric(UsageMeterKind.Images, 2, "images", "Generated images"), new UsageMetric(UsageMeterKind.Unknown, 1, "events", "Mystery")], null)
        ]);

        Assert.Contains("Images,Generated images,2,images,,False,False", csv, StringComparison.Ordinal);
        Assert.Contains("Unknown,Mystery,1,events,,False,True", csv, StringComparison.Ordinal);
    }
}
