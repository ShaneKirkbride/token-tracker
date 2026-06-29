using System.Globalization;
using System.Text;
using AiUsageDashboard.Contracts;

namespace AiUsageDashboard.Web.Services;

public interface ICsvExportService
{
    string ExportUsage(IEnumerable<AiUsageRecord> records);
}

public sealed class CsvExportService : ICsvExportService
{
    public string ExportUsage(IEnumerable<AiUsageRecord> records)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Provider,Region,ModelId,ModelAlias,WindowStart,WindowEnd,MeterKind,MeterName,Quantity,Unit,EstimatedCostUsd,IsPriced,IsUnknown");
        foreach (var record in records)
        {
            foreach (var metric in record.Metrics)
            {
                builder.AppendCsv(record.Provider).Append(',')
                    .AppendCsv(record.Region).Append(',')
                    .AppendCsv(record.ModelId).Append(',')
                    .AppendCsv(record.ModelAlias).Append(',')
                    .Append(record.WindowStart.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.WindowEnd.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                    .Append(metric.Kind).Append(',')
                    .AppendCsv(metric.Name ?? string.Empty).Append(',')
                    .Append(metric.Quantity.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .AppendCsv(metric.Unit).Append(',')
                    .Append(record.EstimatedCostUsd?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                    .Append(record.EstimatedCostUsd.HasValue).Append(',')
                    .AppendLine((metric.Kind == UsageMeterKind.Unknown).ToString());
            }
        }

        return builder.ToString();
    }
}

internal static class CsvBuilderExtensions
{
    public static StringBuilder AppendCsv(this StringBuilder builder, string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            builder.Append('"').Append(value.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
            return builder;
        }

        builder.Append(value);
        return builder;
    }
}
