using AiUsageDashboard.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiUsageDashboard.Providers.GoogleVertex;

public sealed class GoogleVertexUsageProviderOptions
{
    public bool Enabled { get; set; }
    public string[] AllowedProviders { get; set; } = [];
    public string[] AllowedRegions { get; set; } = [];
    public string[] AllowedModels { get; set; } = [];
}

public sealed class GoogleVertexUsageProvider(IOptions<GoogleVertexUsageProviderOptions> options, ILogger<GoogleVertexUsageProvider> logger) : IAiUsageProvider
{
    public string ProviderName => "google-vertex";

    public Task<IReadOnlyList<AiUsageRecord>> GetUsageAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.Value.Enabled)
        {
            return Task.FromResult<IReadOnlyList<AiUsageRecord>>([]);
        }

        logger.LogInformation("{ProviderName} provider integration is enabled for {AllowedRegionCount} configured regions and {AllowedModelCount} configured models. Cloud billing reconciliation is intentionally stubbed.", ProviderName, options.Value.AllowedRegions.Length, options.Value.AllowedModels.Length);
        // TODO: Integrate this provider with cloud-native usage/billing APIs. Do not collect raw prompts/source text; persist usage metadata only.
        return Task.FromResult<IReadOnlyList<AiUsageRecord>>([]);
    }
}
