using System.ComponentModel.DataAnnotations;

namespace AiUsageDashboard.Web.Configuration;

public sealed class ProviderOptions
{
    public const string SectionName = "Providers";
    public ProviderEndpointOptions Gateway { get; set; } = new();
    public CloudWatchBedrockEndpointOptions CloudWatchBedrock { get; set; } = new();
    public CloudWatchBedrockEndpointOptions AwsBedrock { get; set; } = new() { Name = "aws-bedrock" };
    public ProviderEndpointOptions AzureOpenAi { get; set; } = new() { Name = "azure-openai" };
    public ProviderEndpointOptions GoogleVertex { get; set; } = new() { Name = "google-vertex" };
    public ProviderEndpointOptions Mock { get; set; } = new() { Name = "mock" };
}

public class ProviderEndpointOptions
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public Uri? BaseUrl { get; set; }
    public string[] AllowedProviders { get; set; } = [];
    public string[] AllowedRegions { get; set; } = [];
    public string[] AllowedModels { get; set; } = [];
}

public sealed class CloudWatchBedrockEndpointOptions : ProviderEndpointOptions
{
    public CloudWatchBedrockEndpointOptions() => Name = "aws-bedrock";
    public string Region { get; set; } = "us-gov-west-1";
    public string Namespace { get; set; } = "AWS/Bedrock";
}

public sealed class ApprovedModelsOptions
{
    public const string SectionName = "ApprovedModels";
    public ApprovedModelOption[] Models { get; set; } = [];
}

public sealed class ApprovedModelOption
{
    [Required] public string Provider { get; set; } = string.Empty;
    [Required] public string Region { get; set; } = string.Empty;
    [Required] public string ModelId { get; set; } = string.Empty;
    [Required] public string Alias { get; set; } = string.Empty;
    public bool IsApproved { get; set; } = true;
    public bool IsGovCloud { get; set; }
    public string EnvironmentTag { get; set; } = string.Empty;
}

public sealed class ModelPricingOptions
{
    public const string SectionName = "ModelPricing";
    public ModelPriceOption[] Prices { get; set; } = [];
}

public sealed class ModelPriceOption
{
    [Required] public string Provider { get; set; } = string.Empty;
    [Required] public string ModelId { get; set; } = string.Empty;
    [Range(0, double.MaxValue)] public decimal InputPer1MTokensUsd { get; set; }
    [Range(0, double.MaxValue)] public decimal OutputPer1MTokensUsd { get; set; }
    [Range(0, double.MaxValue)] public decimal CachedInputPer1MTokensUsd { get; set; }
}

public sealed class PollingOptions
{
    public const string SectionName = "Polling";
    public bool Enabled { get; set; } = true;
    [Range(1, 1440)] public int IntervalMinutes { get; set; } = 15;
    [Range(1, 10080)] public int LookbackMinutes { get; set; } = 60;
}

public sealed class SecurityOptions
{
    public const string SectionName = "Security";
    public bool DisableAuthForLocalDevelopment { get; set; } = true;
    public string AdminPolicyName { get; set; } = "Admin";
    public string AdminRole { get; set; } = "UsageDashboard.Admin";
}
