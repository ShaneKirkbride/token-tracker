using AiUsageDashboard.Contracts;

namespace AiUsageDashboard.Core;

public sealed class ApprovedModelPolicy(IEnumerable<ApprovedModel> approvedModels)
{
    private readonly HashSet<string> _keys = approvedModels
        .Where(x => x.IsApproved)
        .Select(BuildKey)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool IsApproved(string provider, string region, string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        return _keys.Contains(BuildKey(provider, region, modelId));
    }

    private static string BuildKey(ApprovedModel model) => BuildKey(model.Provider, model.Region, model.ModelId);
    private static string BuildKey(string provider, string region, string modelId) => $"{provider}|{region}|{modelId}";
}
