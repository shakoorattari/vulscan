using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulscan.Application.Common;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;

namespace Vulscan.Infrastructure.BackgroundServices;

/// <summary>
/// Cron-based scheduler. Ticks every minute, computes the next due time per project
/// (project override OR global setting), and enqueues a Queued <see cref="ScanRun"/>
/// when the project's next due time is now-or-past AND it has not already been queued
/// for that occurrence. The poll-based <see cref="ScanBackgroundWorker"/> executes them.
/// </summary>
public sealed class ProjectScanScheduler(
    IServiceScopeFactory scopeFactory,
    ILogger<ProjectScanScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("🗓️  Cron-based project scan scheduler started (tick: {Tick})", TickInterval);

        // Wait until the start of the next minute so ticks align nicely
        var initial = TimeSpan.FromSeconds(60 - DateTime.UtcNow.Second);
        try { await Task.Delay(initial, stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TickInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduler tick failed");
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        var settings = await db.Set<ScheduleSettings>()
            .FirstOrDefaultAsync(s => s.Id == ScheduleSettings.SingletonId, ct);

        if (settings is null || !settings.Enabled)
            return;

        var globalCron = settings.CronExpression;
        var projects = await db.Set<Project>()
            .Where(p => p.IsEnabled)
            .ToListAsync(ct);

        if (projects.Count == 0) return;

        var now = DateTime.UtcNow;
        var queued = 0;

        foreach (var project in projects)
        {
            var cron = project.CronExpression ?? globalCron;

            // Anchor: last time we evaluated/queued for this project. Use LastScannedAt if available,
            // otherwise the project's CreatedAt (so brand-new projects get their first scheduled run).
            var anchor = project.LastScannedAt ?? project.CreatedAt;

            var next = CronExpressionHelper.NextOccurrence(cron, anchor);
            if (next is null || next > now) continue;

            // Skip if a Queued/Running scan already exists for this project
            var hasPending = await db.Set<ScanRun>()
                .AnyAsync(s => s.ProjectId == project.Id &&
                               (s.Status == ScanStatus.Queued || s.Status == ScanStatus.Running), ct);
            if (hasPending) continue;

            db.Set<ScanRun>().Add(new ScanRun
            {
                ProjectId = project.Id,
                Status = ScanStatus.Queued,
                StartedAt = now,
                TriggeredByUserId = null, // system
                CreatedAt = now,
            });
            queued++;
        }

        if (queued > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("📅 Cron tick: queued {Queued}/{Total} project scan(s)", queued, projects.Count);
        }
    }
}
