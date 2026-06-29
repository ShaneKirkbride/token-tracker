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
        builder.AppendLine("Provider,Region,ModelId,ModelAlias,WindowStart,WindowEnd,InputTokens,OutputTokens,CachedInputTokens,Requests,EstimatedCostUsd");
        foreach (var record in records)
        {
            builder.AppendCsv(record.Provider).Append(',')
                .AppendCsv(record.Region).Append(',')
                .AppendCsv(record.ModelId).Append(',')
                .AppendCsv(record.ModelAlias).Append(',')
                .Append(record.WindowStart.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                .Append(record.WindowEnd.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                .Append(record.InputTokens).Append(',')
                .Append(record.OutputTokens).Append(',')
                .Append(record.CachedInputTokens).Append(',')
                .Append(record.Requests).Append(',')
                .AppendLine(record.EstimatedCostUsd.ToString(CultureInfo.InvariantCulture));
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
