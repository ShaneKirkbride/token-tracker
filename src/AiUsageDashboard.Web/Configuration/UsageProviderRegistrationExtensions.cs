using AiUsageDashboard.Contracts;
using AiUsageDashboard.Providers.AwsBedrock;
using AiUsageDashboard.Providers.AzureOpenAI;
using AiUsageDashboard.Providers.Gateway;
using AiUsageDashboard.Providers.GoogleVertex;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiUsageDashboard.Web.Configuration;

public static class UsageProviderRegistrationExtensions
{
    public static IServiceCollection AddConfiguredUsageProviders(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue<bool>($"{ProviderOptions.SectionName}:Gateway:Enabled"))
        {
            services.AddHttpClient<IAiUsageProvider, GatewayUsageProvider>();
        }

        if (IsCloudWatchBedrockEnabled(configuration))
        {
            services.AddSingleton<ICloudWatchBedrockClientFactory, CloudWatchBedrockClientFactory>();
            services.AddSingleton<IAiUsageProvider, CloudWatchBedrockUsageProvider>();
        }

        if (configuration.GetValue<bool>($"{ProviderOptions.SectionName}:AzureOpenAi:Enabled"))
        {
            services.AddSingleton<IAiUsageProvider, AzureOpenAiUsageProvider>();
        }

        if (configuration.GetValue<bool>($"{ProviderOptions.SectionName}:GoogleVertex:Enabled"))
        {
            services.AddSingleton<IAiUsageProvider, GoogleVertexUsageProvider>();
        }

        return services;
    }

    public static bool IsCloudWatchBedrockEnabled(IConfiguration configuration)
    {
        var primaryEnabled = configuration.GetSection($"{ProviderOptions.SectionName}:CloudWatchBedrock").Exists()
            && configuration.GetValue<bool>($"{ProviderOptions.SectionName}:CloudWatchBedrock:Enabled");
        var legacyEnabled = configuration.GetValue<bool>($"{ProviderOptions.SectionName}:AwsBedrock:Enabled");
        return primaryEnabled || legacyEnabled;
    }
}
