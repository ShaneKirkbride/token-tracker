using AiUsageDashboard.Contracts;

namespace AiUsageDashboard.Storage;

public sealed class UsageSnapshot
{
    public long Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string ModelAlias { get; set; } = string.Empty;
    public DateTimeOffset WindowStart { get; set; }
    public DateTimeOffset WindowEnd { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long CachedInputTokens { get; set; }
    public int Requests { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    public AiUsageRecord ToRecord() => new(Provider, Region, ModelId, ModelAlias, WindowStart, WindowEnd, InputTokens, OutputTokens, CachedInputTokens, Requests, EstimatedCostUsd);

    public static UsageSnapshot FromRecord(AiUsageRecord record, DateTimeOffset capturedAt) => new()
    {
        Provider = record.Provider,
        Region = record.Region,
        ModelId = record.ModelId,
        ModelAlias = record.ModelAlias,
        WindowStart = record.WindowStart,
        WindowEnd = record.WindowEnd,
        InputTokens = record.InputTokens,
        OutputTokens = record.OutputTokens,
        CachedInputTokens = record.CachedInputTokens,
        Requests = record.Requests,
        EstimatedCostUsd = record.EstimatedCostUsd,
        CapturedAt = capturedAt
    };
}

public sealed class ApprovedModelEntity
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public bool IsGovCloud { get; set; }
    public string EnvironmentTag { get; set; } = string.Empty;
}

public sealed class ModelPriceEntity
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public decimal InputPer1MTokensUsd { get; set; }
    public decimal OutputPer1MTokensUsd { get; set; }
    public decimal CachedInputPer1MTokensUsd { get; set; }
}
