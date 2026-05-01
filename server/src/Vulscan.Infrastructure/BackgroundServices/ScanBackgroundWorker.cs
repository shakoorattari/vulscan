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

        // Recover orphaned "Running" scans left behind by a previous crash/restart.
        // They are re-queued so this process can pick them up cleanly.
        await RecoverOrphanedScansAsync(stoppingToken);

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

    /// <summary>
    /// On startup, requeue any scans left in the Running state by a previous
    /// process (crash, restart, etc.). They will be picked up by the next poll.
    /// </summary>
    private async Task RecoverOrphanedScansAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();

            var orphans = await db.Set<ScanRun>()
                .Where(s => s.Status == ScanStatus.Running)
                .ToListAsync(ct);

            if (orphans.Count == 0) return;

            foreach (var s in orphans)
            {
                s.Status = ScanStatus.Queued;
                s.CompletedAt = null;
                s.ErrorLog = null;
            }
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Recovered {Count} orphaned Running scan(s) → re-queued", orphans.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to recover orphaned scans");
        }
    }
}
