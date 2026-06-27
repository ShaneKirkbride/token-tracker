using AiUsageDashboard.Contracts;
using AiUsageDashboard.Core;
using AiUsageDashboard.Providers.Mock;
using AiUsageDashboard.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<TokenCostEstimator>();
builder.Services.AddSingleton<IAiUsageProvider, MockUsageProvider>();
builder.Services.AddSingleton<UsageDashboardService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
