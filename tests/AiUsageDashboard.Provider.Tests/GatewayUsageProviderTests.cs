using System.Net;
using AiUsageDashboard.Providers.AwsBedrock;
using AiUsageDashboard.Providers.Gateway;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiUsageDashboard.Provider.Tests;

public sealed class GatewayUsageProviderTests
{
    [Fact]
    public async Task GetUsageAsync_ParsesGatewayJson()
    {
        using var client = new HttpClient(new StubHandler("""
            [{"provider":"gateway","region":"us-gov-west-1","modelId":"m","modelAlias":"Model","windowStart":"2026-06-01T00:00:00+00:00","windowEnd":"2026-06-01T01:00:00+00:00","inputTokens":10,"outputTokens":5,"cachedInputTokens":2,"requests":1,"estimatedCostUsd":0.5}]
            """)) { BaseAddress = new Uri("https://example.test/") };
        var provider = new GatewayUsageProvider(client, Options.Create(new GatewayUsageProviderOptions { Enabled = true, BaseUrl = new Uri("https://example.test/") }), NullLogger<GatewayUsageProvider>.Instance);

        var records = await provider.GetUsageAsync(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), DateTimeOffset.Parse("2026-06-01T01:00:00Z"), CancellationToken.None);

        Assert.Single(records);
        Assert.Equal("gateway", records[0].Provider);
    }


    [Fact]
    public async Task GetUsageAsync_ReturnsEmptyForNullPayload()
    {
        using var client = new HttpClient(new StubHandler("null"));
        var provider = new GatewayUsageProvider(client, Options.Create(new GatewayUsageProviderOptions { Enabled = true, BaseUrl = new Uri("https://example.test/") }), NullLogger<GatewayUsageProvider>.Instance);

        var records = await provider.GetUsageAsync(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Empty(records);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsEmptyOnHttpFailure()
    {
        using var client = new HttpClient(new StubHandler("", HttpStatusCode.InternalServerError));
        var provider = new GatewayUsageProvider(client, Options.Create(new GatewayUsageProviderOptions { Enabled = true, BaseUrl = new Uri("https://example.test/") }), NullLogger<GatewayUsageProvider>.Instance);

        var records = await provider.GetUsageAsync(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Empty(records);
    }


    [Fact]
    public async Task GetUsageAsync_ReturnsEmptyWhenDisabled()
    {
        using var client = new HttpClient(new StubHandler("[]"));
        var provider = new GatewayUsageProvider(client, Options.Create(new GatewayUsageProviderOptions { Enabled = false }), NullLogger<GatewayUsageProvider>.Instance);

        var records = await provider.GetUsageAsync(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Empty(records);
    }

    [Fact]
    public async Task GetUsageAsync_ThrowsWhenEnabledWithoutBaseUrl()
    {
        using var client = new HttpClient(new StubHandler("[]"));
        var provider = new GatewayUsageProvider(client, Options.Create(new GatewayUsageProviderOptions { Enabled = true }), NullLogger<GatewayUsageProvider>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetUsageAsync(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task StubProvider_ReturnsNoPromptData()
    {
        var provider = new AwsBedrockUsageProvider(Options.Create(new AwsBedrockUsageProviderOptions { Enabled = true, AllowedModels = ["m"] }), NullLogger<AwsBedrockUsageProvider>.Instance);
        var records = await provider.GetUsageAsync(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Empty(records);
    }


    [Fact]
    public async Task GetUsageAsync_RethrowsRequestedCancellation()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        using var client = new HttpClient(new CancelingHandler());
        var provider = new GatewayUsageProvider(client, Options.Create(new GatewayUsageProviderOptions { Enabled = true, BaseUrl = new Uri("https://example.test/") }), NullLogger<GatewayUsageProvider>.Instance);

        Assert.Equal("gateway", provider.ProviderName);
        var exception = await Record.ExceptionAsync(() => provider.GetUsageAsync(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, source.Token));
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    private sealed class CancelingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new OperationCanceledException(cancellationToken);
    }

    private sealed class StubHandler(string json, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Contains("/admin/usage", request.RequestUri!.PathAndQuery, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(json) });
        }
    }
}
