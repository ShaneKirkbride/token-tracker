using AiUsageDashboard.Contracts;
using AiUsageDashboard.Core;
using AiUsageDashboard.Providers.AwsBedrock;
using AiUsageDashboard.Providers.AzureOpenAI;
using AiUsageDashboard.Providers.Gateway;
using AiUsageDashboard.Providers.GoogleVertex;
using AiUsageDashboard.Storage;
using AiUsageDashboard.Web.Components;
using AiUsageDashboard.Web.Configuration;
using AiUsageDashboard.Web.Services;
using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");

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
builder.Services.AddSingleton(serviceProvider =>
{
    var models = serviceProvider.GetRequiredService<IOptions<ApprovedModelsOptions>>().Value.Models
        .Select(model => new ApprovedModel(model.Provider, model.Region, model.ModelId, model.Alias, model.IsApproved, model.IsGovCloud, model.EnvironmentTag))
        .ToArray();
    return new ApprovedModelPolicy(models);
});
builder.Services.AddScoped<UsageDashboardService>();
builder.Services.AddMockUsageProviderIfEnabled(builder.Configuration);

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

app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UsageDashboardDbContext>();
    await db.Database.EnsureCreatedAsync();
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

public partial class Program;
