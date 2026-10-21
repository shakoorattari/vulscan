using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulscan.Application.Interfaces;
using Vulscan.Infrastructure.Intel;

namespace Vulscan.Infrastructure.BackgroundServices;

/// <summary>Wakes the background refresher early (a scan just finished) instead of waiting out its interval.</summary>
public sealed class IntelTrigger : IIntelTrigger
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Request()
    {
        try { _signal.Release(); }
        catch (SemaphoreFullException) { /* a wake-up is already pending */ }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _signal.WaitAsync(timeout, ct);
}

/// <summary>
/// Keeps vulnerability intelligence (CVSS, dates, references, fixes, KEV, EPSS) filled in for every finding the
/// latest scans report, without anyone having to open each CVE page. Never blocks a scan: scans only nudge it.
/// </summary>
public sealed class IntelBackgroundWorker(
    IServiceScopeFactory scopes,
    IntelTrigger trigger,
    IOptions<IntelOptions> options,
    ILogger<IntelBackgroundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Let the API finish starting (and migrate the database) before the first pass.
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (options.Value.Enabled)
                {
                    try
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        var refresher = scope.ServiceProvider.GetRequiredService<IntelRefresher>();
                        await refresher.RefreshPendingAsync(stoppingToken);
                        await refresher.SweepSignalsAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Intel refresh pass failed; will retry next interval");
                    }
                }

                await trigger.WaitAsync(TimeSpan.FromMinutes(Math.Max(1, options.Value.WorkerIntervalMinutes)), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
