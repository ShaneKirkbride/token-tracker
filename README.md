# token-tracker / AiUsageDashboard

Production-ready skeleton for a Blazor/.NET 10 AI usage dashboard that tracks approved model token usage, request volume, and estimated cost across operational gateway and cloud-provider telemetry.

## Solution layout

- `src/AiUsageDashboard.Contracts` - provider-neutral records and interfaces.
- `src/AiUsageDashboard.Core` - cost estimation, approved-model policy, provider orchestration, summary grouping.
- `src/AiUsageDashboard.Storage` - EF Core SQLite storage, metadata entities, repositories, and dashboard query service.
- `src/AiUsageDashboard.Providers.Gateway` - HttpClientFactory-ready gateway telemetry provider for `GET {BaseUrl}/admin/usage?from={from:o}&to={to:o}`.
- `src/AiUsageDashboard.Providers.AwsBedrock` - GovCloud-ready CloudWatch Bedrock usage provider for approved Jarvis1 models.
- `src/AiUsageDashboard.Providers.AzureOpenAI` - Azure OpenAI extension point; intentionally metadata-only stub.
- `src/AiUsageDashboard.Providers.GoogleVertex` - Google Vertex extension point; intentionally metadata-only stub.
- `src/AiUsageDashboard.Web` - dark Blazor dashboard, polling, auth-ready structure, CSV export, config, and local SQLite hosting.
- `tests/*` - xUnit coverage for core, provider, storage, and web support logic.

## Run locally

```bash
dotnet restore AiUsageDashboard.sln
dotnet build AiUsageDashboard.sln -c Release --no-restore
dotnet run --project src/AiUsageDashboard.Web/AiUsageDashboard.Web.csproj
```

Local development authentication is enabled by default and grants the `UsageDashboard.Admin` role. Disable it only after configuring enterprise OIDC placeholders under `Authentication:EnterpriseOidc`.

## Test with coverage gate

```bash
dotnet test AiUsageDashboard.sln -c Release --no-restore /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=94 /p:ThresholdType=line /p:ThresholdStat=total
```

Coverage excludes Blazor rendering glue, startup wiring, EF DbContext mapping boilerplate, and intentional cloud-provider stubs. Core, gateway provider, storage repository/query logic, configuration validators, and CSV formatting are covered by executable tests.

## Configuration

Configuration lives in `src/AiUsageDashboard.Web/appsettings.json` and can be overridden with `appsettings.Development.json` or environment variables.

Key sections:

- `Providers` - enable/disable gateway and cloud providers; configure base URLs and allowlists.
- `ApprovedModels` - provider/region/model/deployment allowlist driving dashboard visibility.
- `ModelPricing` - estimated pricing per 1M input, output, and cached input tokens.
- `Polling` - background polling interval and lookback window.
- `Security` - local development auth toggle and Admin policy role.

Provider clients must collect and persist usage metadata only. Raw prompts, source code, and response bodies are out of scope for storage.

## ECS CloudWatch Bedrock polling

Normal Jarvis1 ECS deployments should poll AWS CloudWatch Bedrock metrics directly instead of the gateway `/admin/usage` endpoint. Use the primary `Providers:CloudWatchBedrock` section for CloudWatch metric polling; the older `Providers:AwsBedrock` section is retained only as a backwards-compatible alias.

```text
Providers__CloudWatchBedrock__Enabled=true
Providers__CloudWatchBedrock__Region=us-gov-west-1
Providers__CloudWatchBedrock__Namespace=AWS/Bedrock

Providers__Gateway__Enabled=false
Providers__Mock__Enabled=false
Providers__AwsBedrock__Enabled=false
Providers__AzureOpenAi__Enabled=false
Providers__GoogleVertex__Enabled=false

Polling__LookbackMinutes=10080
Polling__IntervalMinutes=15
```

The default approved Bedrock model allowlist is limited to:

- `openai.gpt-oss-120b-1:0`
- `meta.llama3-70b-instruct-v1:0`

## Gateway telemetry

Gateway telemetry remains available only when explicitly enabled:

```http
GET /admin/usage?from=2026-06-01T00:00:00Z&to=2026-06-27T23:59:59Z
```

Cloud-provider billing integrations are intentionally stubbed for later reconciliation work because billing APIs can lag and usually provide less operational granularity than gateway telemetry.

## CI and container

GitHub Actions in `.github/workflows/ci.yml` runs restore, build, coverage-gated tests, and publishes a web artifact. The root `Dockerfile` publishes and runs the Blazor app on port `8080`.
