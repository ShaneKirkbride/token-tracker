using AiUsageDashboard.Contracts;
using AiUsageDashboard.Core;
using AiUsageDashboard.Providers.AwsBedrock;
using AiUsageDashboard.Providers.AzureOpenAI;
using AiUsageDashboard.Providers.Gateway;
using AiUsageDashboard.Providers.GoogleVertex;
using AiUsageDashboard.Providers.Mock;
using AiUsageDashboard.Storage;
using AiUsageDashboard.Web.Components;
using AiUsageDashboard.Web.Configuration;
using AiUsageDashboard.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOptions<ProviderOptions>().Bind(builder.Configuration.GetSection(ProviderOptions.SectionName)).ValidateOnStart();
builder.Services.AddOptions<ApprovedModelsOptions>().Bind(builder.Configuration.GetSection(ApprovedModelsOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<ModelPricingOptions>().Bind(builder.Configuration.GetSection(ModelPricingOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<PollingOptions>().Bind(builder.Configuration.GetSection(PollingOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<SecurityOptions>().Bind(builder.Configuration.GetSection(SecurityOptions.SectionName)).ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ProviderOptions>, ProviderOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<ApprovedModelsOptions>, ApprovedModelsOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<ModelPricingOptions>, ModelPricingOptionsValidator>();

var connectionString = builder.Configuration.GetConnectionString("UsageDashboard") ?? "Data Source=usage-dashboard.db";
builder.Services.AddDbContext<UsageDashboardDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<IUsageSnapshotRepository, EfUsageSnapshotRepository>();
builder.Services.AddScoped<IDashboardQueryService, DashboardQueryService>();
builder.Services.AddSingleton<ICsvExportService, CsvExportService>();

builder.Services.AddSingleton<TokenCostEstimator>();
builder.Services.AddScoped<UsageDashboardService>();
builder.Services.AddSingleton<IAiUsageProvider, MockUsageProvider>();

builder.Services.AddOptions<GatewayUsageProviderOptions>().Configure<IOptions<ProviderOptions>>((gateway, providers) =>
{
    gateway.BaseUrl = providers.Value.Gateway.BaseUrl;
    gateway.Enabled = providers.Value.Gateway.Enabled;
});
builder.Services.AddOptions<AwsBedrockUsageProviderOptions>().Configure<IOptions<ProviderOptions>>((aws, providers) =>
{
    aws.Enabled = providers.Value.AwsBedrock.Enabled;
    aws.AllowedProviders = providers.Value.AwsBedrock.AllowedProviders;
    aws.AllowedRegions = providers.Value.AwsBedrock.AllowedRegions;
    aws.AllowedModels = providers.Value.AwsBedrock.AllowedModels;
});
builder.Services.AddOptions<AzureOpenAiUsageProviderOptions>().Configure<IOptions<ProviderOptions>>((azure, providers) =>
{
    azure.Enabled = providers.Value.AzureOpenAi.Enabled;
    azure.AllowedProviders = providers.Value.AzureOpenAi.AllowedProviders;
    azure.AllowedRegions = providers.Value.AzureOpenAi.AllowedRegions;
    azure.AllowedModels = providers.Value.AzureOpenAi.AllowedModels;
});
builder.Services.AddOptions<GoogleVertexUsageProviderOptions>().Configure<IOptions<ProviderOptions>>((google, providers) =>
{
    google.Enabled = providers.Value.GoogleVertex.Enabled;
    google.AllowedProviders = providers.Value.GoogleVertex.AllowedProviders;
    google.AllowedRegions = providers.Value.GoogleVertex.AllowedRegions;
    google.AllowedModels = providers.Value.GoogleVertex.AllowedModels;
});

builder.Services.AddHttpClient<IAiUsageProvider, GatewayUsageProvider>();
builder.Services.AddSingleton<IAiUsageProvider, AwsBedrockUsageProvider>();
builder.Services.AddSingleton<IAiUsageProvider, AzureOpenAiUsageProvider>();
builder.Services.AddSingleton<IAiUsageProvider, GoogleVertexUsageProvider>();
builder.Services.AddHostedService<UsagePollingService>();

var security = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();
var auth = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = security.DisableAuthForLocalDevelopment ? LocalDevelopmentAuthHandler.SchemeName : "EnterpriseOidc";
});
if (security.DisableAuthForLocalDevelopment)
{
    auth.AddScheme<AuthenticationSchemeOptions, LocalDevelopmentAuthHandler>(LocalDevelopmentAuthHandler.SchemeName, _ => { });
}
else
{
    auth.AddOpenIdConnect("EnterpriseOidc", options => builder.Configuration.GetSection("Authentication:EnterpriseOidc").Bind(options));
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(security.AdminPolicyName, policy => policy.RequireRole(security.AdminRole));
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UsageDashboardDbContext>();
    if (await HasLegacyTokenOnlySchemaAsync(db))
    {
        await MigrateLegacyTokenSchemaAsync(db);
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/admin/export.csv", async (DateTimeOffset from, DateTimeOffset to, IDashboardQueryService dashboard, ICsvExportService csv, CancellationToken cancellationToken) =>
{
    var data = await dashboard.GetDashboardAsync(from, to, cancellationToken);
    return Results.Text(csv.ExportUsage(data.Records), "text/csv");
}).RequireAuthorization(security.AdminPolicyName);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization(security.AdminPolicyName);

app.Run();

static async Task<bool> HasLegacyTokenOnlySchemaAsync(UsageDashboardDbContext db)
{
    var connection = (SqliteConnection)db.Database.GetDbConnection();
    await connection.OpenAsync();
    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'UsageSnapshots';";
        var hasSnapshots = Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
        if (!hasSnapshots)
        {
            return false;
        }

        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'UsageMetrics';";
        return Convert.ToInt64(await command.ExecuteScalarAsync()) == 0;
    }
    finally
    {
        await connection.CloseAsync();
    }
}

static async Task MigrateLegacyTokenSchemaAsync(UsageDashboardDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("ALTER TABLE UsageSnapshots RENAME TO UsageSnapshots_Legacy;");
    await CreateMeterSchemaTablesAsync(db);
    await db.Database.ExecuteSqlRawAsync("""
        INSERT INTO UsageSnapshots (Id, Provider, Region, ModelId, ModelAlias, WindowStart, WindowEnd, EstimatedCostUsd, CapturedAt)
        SELECT Id, Provider, Region, ModelId, ModelAlias, WindowStart, WindowEnd, EstimatedCostUsd, CapturedAt
        FROM UsageSnapshots_Legacy;
        """);
    await db.Database.ExecuteSqlRawAsync("""
        INSERT INTO UsageMetrics (UsageSnapshotId, Kind, Quantity, Unit, Name)
        SELECT Id, 'InputTokens', InputTokens, 'tokens', 'Input tokens' FROM UsageSnapshots_Legacy
        UNION ALL SELECT Id, 'OutputTokens', OutputTokens, 'tokens', 'Output tokens' FROM UsageSnapshots_Legacy
        UNION ALL SELECT Id, 'CachedInputTokens', CachedInputTokens, 'tokens', 'Cached input tokens' FROM UsageSnapshots_Legacy
        UNION ALL SELECT Id, 'TotalTokens', InputTokens + OutputTokens + CachedInputTokens, 'tokens', 'Total tokens' FROM UsageSnapshots_Legacy
        UNION ALL SELECT Id, 'Requests', Requests, 'requests', 'Requests' FROM UsageSnapshots_Legacy;
        """);
    await db.Database.ExecuteSqlRawAsync("DROP TABLE UsageSnapshots_Legacy;");
}

static async Task CreateMeterSchemaTablesAsync(UsageDashboardDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "ModelMeterPrices" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_ModelMeterPrices" PRIMARY KEY AUTOINCREMENT,
            "Provider" TEXT NOT NULL,
            "ModelId" TEXT NOT NULL,
            "MeterKind" TEXT NOT NULL,
            "PriceUsd" TEXT NOT NULL,
            "UnitQuantity" TEXT NOT NULL,
            "Unit" TEXT NOT NULL
        );
        """);
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "ModelQuotas" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_ModelQuotas" PRIMARY KEY AUTOINCREMENT,
            "Provider" TEXT NOT NULL,
            "Region" TEXT NOT NULL,
            "ModelId" TEXT NOT NULL,
            "MeterKind" TEXT NOT NULL,
            "Limit" TEXT NOT NULL,
            "Window" TEXT NOT NULL,
            "QuotaName" TEXT NOT NULL,
            "Unit" TEXT NOT NULL
        );
        """);
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "UsageSnapshots" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_UsageSnapshots" PRIMARY KEY AUTOINCREMENT,
            "Provider" TEXT NOT NULL,
            "Region" TEXT NOT NULL,
            "ModelId" TEXT NOT NULL,
            "ModelAlias" TEXT NOT NULL,
            "WindowStart" TEXT NOT NULL,
            "WindowEnd" TEXT NOT NULL,
            "EstimatedCostUsd" TEXT NULL,
            "CapturedAt" TEXT NOT NULL
        );
        """);
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "UsageMetrics" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_UsageMetrics" PRIMARY KEY AUTOINCREMENT,
            "UsageSnapshotId" INTEGER NOT NULL,
            "Kind" TEXT NOT NULL,
            "Quantity" TEXT NOT NULL,
            "Unit" TEXT NOT NULL,
            "Name" TEXT NULL,
            CONSTRAINT "FK_UsageMetrics_UsageSnapshots_UsageSnapshotId" FOREIGN KEY ("UsageSnapshotId") REFERENCES "UsageSnapshots" ("Id") ON DELETE CASCADE
        );
        """);
    await db.Database.ExecuteSqlRawAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_ModelMeterPrices_Provider_ModelId_MeterKind" ON "ModelMeterPrices" ("Provider", "ModelId", "MeterKind");""");
    await db.Database.ExecuteSqlRawAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_ModelQuotas_Provider_Region_ModelId_MeterKind_QuotaName" ON "ModelQuotas" ("Provider", "Region", "ModelId", "MeterKind", "QuotaName");""");
    await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_UsageMetrics_UsageSnapshotId_Kind" ON "UsageMetrics" ("UsageSnapshotId", "Kind");""");
    await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_UsageSnapshots_Provider_Region_ModelId_WindowStart_WindowEnd" ON "UsageSnapshots" ("Provider", "Region", "ModelId", "WindowStart", "WindowEnd");""");
}

public partial class Program;
