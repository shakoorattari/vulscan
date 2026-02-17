using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;

namespace Vulscan.Infrastructure.BackgroundServices;

/// <summary>
/// Background worker that polls for queued scans and processes them.
/// </summary>
public sealed class ScanBackgroundWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ScanBackgroundWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scan background worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueuedScansAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error in scan background worker");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        logger.LogInformation("Scan background worker stopped");
    }

    private async Task ProcessQueuedScansAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
        var scanProcessor = scope.ServiceProvider.GetRequiredService<IScanProcessor>();

        // Find queued scans
        var queuedScan = await dbContext.Set<ScanRun>()
            .Where(s => s.Status == ScanStatus.Queued)
            .OrderBy(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (queuedScan == null)
            return;

        logger.LogInformation("Processing queued scan {ScanId}", queuedScan.Id);

        try
        {
            await scanProcessor.ProcessScanAsync(queuedScan.Id, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process scan {ScanId}", queuedScan.Id);

            // Mark as failed
            queuedScan.Status = ScanStatus.Failed;
            queuedScan.CompletedAt = DateTime.UtcNow;
            queuedScan.ErrorLog = ex.Message;
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
