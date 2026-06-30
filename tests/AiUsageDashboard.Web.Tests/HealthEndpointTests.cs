using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AiUsageDashboard.Web.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:UsageDashboard", "Data Source=:memory:");
            builder.UseSetting("Polling:Enabled", "false");
            builder.UseSetting("Providers:Mock:Enabled", "false");
        });
    }

    [Fact]
    public async Task Healthz_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
