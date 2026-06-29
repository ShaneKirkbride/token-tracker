using AiUsageDashboard.Core;
using AiUsageDashboard.Storage;
using AiUsageDashboard.Web.Configuration;
using Microsoft.Extensions.Options;

namespace AiUsageDashboard.Web.Services;

public sealed class UsagePollingService(IServiceScopeFactory scopeFactory, IOptions<PollingOptions> options, ILogger<UsagePollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Usage polling is disabled by configuration.");
            return;
        }

        await PollOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.IntervalMinutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PollOnceAsync(stoppingToken);
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var to = DateTimeOffset.UtcNow;
        var from = to.AddMinutes(-options.Value.LookbackMinutes);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var usage = scope.ServiceProvider.GetRequiredService<UsageDashboardService>();
            var repository = scope.ServiceProvider.GetRequiredService<IUsageSnapshotRepository>();
            var records = await usage.GetUsageAsync(from, to, cancellationToken);
            await repository.StoreAsync(records, to, cancellationToken);
            logger.LogInformation("Stored {RecordCount} usage metadata records for {From:o} to {To:o}.", records.Count, from, to);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Usage polling failed for {From:o} to {To:o}; the app will continue running.", from, to);
        }
    }
}
