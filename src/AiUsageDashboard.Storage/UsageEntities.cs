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
    public decimal? EstimatedCostUsd { get; set; }
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<UsageMetricEntity> Metrics { get; set; } = [];

    public long InputTokens => WholeQuantity(UsageMeterKind.InputTokens);
    public long OutputTokens => WholeQuantity(UsageMeterKind.OutputTokens);
    public long CachedInputTokens => WholeQuantity(UsageMeterKind.CachedInputTokens);
    public int Requests => (int)WholeQuantity(UsageMeterKind.Requests);

    public AiUsageRecord ToRecord() => new(Provider, Region, ModelId, ModelAlias, WindowStart, WindowEnd, Metrics.Select(x => x.ToMetric()).ToArray(), EstimatedCostUsd);

    public static UsageSnapshot FromRecord(AiUsageRecord record, DateTimeOffset capturedAt) => new()
    {
        Provider = record.Provider,
        Region = record.Region,
        ModelId = record.ModelId,
        ModelAlias = record.ModelAlias,
        WindowStart = record.WindowStart,
        WindowEnd = record.WindowEnd,
        EstimatedCostUsd = record.EstimatedCostUsd,
        CapturedAt = capturedAt,
        Metrics = record.Metrics.Select(UsageMetricEntity.FromMetric).ToList()
    };

    private long WholeQuantity(UsageMeterKind kind) => (long)Metrics.Where(x => x.Kind == kind).Sum(x => x.Quantity);
}

public sealed class UsageMetricEntity
{
    public long Id { get; set; }
    public long UsageSnapshotId { get; set; }
    public UsageMeterKind Kind { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Name { get; set; }

    public UsageMetric ToMetric() => new(Kind, Quantity, Unit, Name);
    public static UsageMetricEntity FromMetric(UsageMetric metric) => new() { Kind = metric.Kind, Quantity = metric.Quantity, Unit = metric.Unit, Name = metric.Name };
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

public sealed class ModelMeterPriceEntity
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public UsageMeterKind MeterKind { get; set; }
    public decimal PriceUsd { get; set; }
    public decimal UnitQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}


public sealed class ModelQuotaEntity
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public UsageMeterKind MeterKind { get; set; }
    public decimal Limit { get; set; }
    public TimeSpan Window { get; set; }
    public string QuotaName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    public ModelQuota ToQuota() => new(Provider, Region, ModelId, MeterKind, Limit, Window, QuotaName, Unit);
}
