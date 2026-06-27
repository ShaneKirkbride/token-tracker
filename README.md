# token-tracker

Blazor/.NET 10 dashboard for tracking approved AI model token usage, request volume, and estimated cost across cloud providers.

## What is included

- `AiUsageDashboard.Web` - Blazor Web App with dashboard cards, token bars, provider split graphic, and usage table.
- `AiUsageDashboard.Contracts` - provider-neutral records and `IAiUsageProvider`.
- `AiUsageDashboard.Core` - cost estimation, model approval policy, usage aggregation.
- `AiUsageDashboard.Providers.Mock` - deterministic provider for local demos/tests.
- `AiUsageDashboard.Core.Tests` - xUnit tests with Coverlet coverage threshold set to 94% line coverage.

## Run locally

```powershell
dotnet restore .\AiUsageDashboard.sln
dotnet run --project .\src\AiUsageDashboard.Web\AiUsageDashboard.Web.csproj
```

## Test with coverage gate

```powershell
dotnet test .\AiUsageDashboard.sln -c Release /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=94 /p:ThresholdType=line /p:ThresholdStat=total
```

## Production path

For Jarvis/GovCloud usage, make the AI Gateway the operational source of truth:

```http
GET /admin/usage?from=2026-06-01T00:00:00Z&to=2026-06-27T23:59:59Z
```

Then implement real providers behind `IAiUsageProvider`:

- `AwsBedrockUsageProvider` - CloudWatch metrics/logs or gateway audit table reconciliation.
- `AzureOpenAiUsageProvider` - gateway audit table plus Azure cost reconciliation.
- `GoogleVertexUsageProvider` - BigQuery billing export plus gateway audit data.

Cloud billing APIs should be treated as reconciliation because they can lag and usually do not provide the same operational granularity as gateway telemetry.

## Current status

This is a starter solution/prototype. The next production steps are persistence, real provider implementations, auth, appsettings-based model/pricing configuration, CI, and real charting.
