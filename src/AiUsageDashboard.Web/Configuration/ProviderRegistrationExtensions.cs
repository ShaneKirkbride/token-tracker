using AiUsageDashboard.Contracts;
using AiUsageDashboard.Providers.Mock;

namespace AiUsageDashboard.Web.Configuration;

public static class ProviderRegistrationExtensions
{
    public static IServiceCollection AddMockUsageProviderIfEnabled(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.GetValue<bool>($"{ProviderOptions.SectionName}:Mock:Enabled"))
        {
            services.AddSingleton<IAiUsageProvider, MockUsageProvider>();
        }

        return services;
    }
}
