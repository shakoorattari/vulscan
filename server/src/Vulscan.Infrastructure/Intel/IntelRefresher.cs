using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulscan.Application.Common;
using Vulscan.Application.DTOs.Intel;
using Vulscan.Application.Intel;
using Vulscan.Domain.Entities;
using Vulscan.Infrastructure.Clients;
using Vulscan.Infrastructure.Data;

namespace Vulscan.Infrastructure.Intel;

/// <summary>
/// The background half of vulnerability intelligence: fetches advisories the portal has not seen yet, refreshes
/// stale ones, and keeps the two fast-moving signals — CISA KEV listing and EPSS — current for everything stored.
/// </summary>
public sealed class IntelRefresher(
    VulscanDbContext db,
    IServiceScopeFactory scopes,
    IEpssApiClient epss,
    IKevCatalogClient kev,
    IOptions<IntelOptions> options,
    TimeProvider time,
    ILogger<IntelRefresher> logger)
{
    private const int PageSize = 200;
    private static readonly TimeSpan EpssRecheck = TimeSpan.FromHours(20);   // EPSS publishes new scores daily

    /// <summary>
    /// Advisories found by each project's current scan that have no stored record, or whose record is due for a
    /// refresh. Unseen ones come first.
    /// </summary>
    public async Task<IReadOnlyList<string>> FindPendingAsync(int max, CancellationToken ct = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var schema = VulnerabilityIntelDto.CurrentSchemaVersion;

        var scanIds = await CurrentScans.IdsAsync(db, ct);
        if (scanIds.Count == 0) return [];

        var wanted = db.Vulnerabilities.Where(v => scanIds.Contains(v.ScanRunId)).Select(v => v.CveId).Distinct();

        var missing = await wanted
            .Where(id => !db.VulnerabilityIntel.Any(i => i.AdvisoryId == id))
            .Take(max).ToListAsync(ct);

        var remaining = max - missing.Count;
        var stale = remaining <= 0
            ? []
            : await wanted
                .Where(id => db.VulnerabilityIntel.Any(i => i.AdvisoryId == id && (i.RefreshAfter <= now || i.SchemaVersion != schema)))
                .Take(remaining).ToListAsync(ct);

        return missing.Concat(stale).Where(IntelId.IsValid).ToList();
    }

    /// <summary>One pass of the refresher. Returns how many advisories were fetched.</summary>
    public async Task<int> RefreshPendingAsync(CancellationToken ct = default)
    {
        var cfg = options.Value;
        if (!cfg.Enabled) return 0;

        var pending = await FindPendingAsync(cfg.MaxPerCycle, ct);
        if (pending.Count == 0) return 0;
        logger.LogInformation("Intel: fetching {Count} advisories", pending.Count);

        // Bounded parallelism. Each fetch gets its own scope, and so its own DbContext (a DbContext is not
        // thread-safe). NVD's rate limiter is shared process-wide, so more parallelism cannot exceed its allowance.
        using var gate = new SemaphoreSlim(Math.Max(1, cfg.Concurrency));
        var done = 0;
        await Task.WhenAll(pending.Select(async id =>
        {
            await gate.WaitAsync(ct);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<VulnerabilityIntelService>().RefreshAsync(id, ct);
                Interlocked.Increment(ref done);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Intel: refreshing {Id} failed", id);
            }
            finally
            {
                gate.Release();
            }
        }));

        return done;
    }

    /// <summary>
    /// Brings KEV listing and EPSS scores up to date on every stored CVE. KEV changes several times a week and EPSS
    /// daily, so waiting for each record's own 24-hour refresh would leave an exploited CVE looking unlisted.
    /// </summary>
    public async Task<(int Kev, int Epss)> SweepSignalsAsync(CancellationToken ct = default)
    {
        var cfg = options.Value;
        if (!cfg.Enabled) return (0, 0);

        var now = time.GetUtcNow().UtcDateTime;
        var catalog = await kev.GetCatalogAsync(ct);
        int kevChanged = 0, epssChanged = 0;

        for (var page = 0; ; page++)
        {
            var rows = await db.VulnerabilityIntel
                .Where(r => r.Status != IntelStatus.NotFound && r.AdvisoryId.StartsWith("CVE-"))
                .OrderBy(r => r.AdvisoryId).Skip(page * PageSize).Take(PageSize)
                .ToListAsync(ct);
            if (rows.Count == 0) break;

            var docs = rows.Select(r => (Row: r, Doc: VulnerabilityIntelService.Deserialize(r)))
                .Where(x => x.Doc is not null && x.Row.SchemaVersion == VulnerabilityIntelDto.CurrentSchemaVersion)
                .Select(x => (x.Row, Doc: x.Doc!)).ToList();

            // Which of this page's CVEs need a fresh EPSS score?
            var needEpss = docs
                .Where(x => x.Doc.Sources.FirstOrDefault(s => s.Name == "EPSS") is not { FetchedAt: { } at } || now - at > EpssRecheck)
                .Select(x => x.Row.AdvisoryId).ToList();
            var epssResult = needEpss.Count > 0 ? await epss.GetScoresAsync(needEpss, ct) : null;

            foreach (var (row, doc) in docs)
            {
                var updated = doc;

                if (catalog.IsOk && catalog.Value is { } cat)
                {
                    var newKev = cat.Entries.TryGetValue(row.AdvisoryId, out var entry) ? IntelMerger.ToKevDto(entry) : null;
                    if (newKev != doc.Kev)
                    {
                        updated = updated with { Kev = newKev };
                        kevChanged++;
                    }
                }

                if (epssResult is { IsOk: true, Value: { } scores } && needEpss.Contains(row.AdvisoryId))
                {
                    var found = scores.TryGetValue(row.AdvisoryId, out var s);
                    var newEpss = found ? new EpssDto(s!.Score, s.Percentile, s.Date) : updated.Epss;
                    if (newEpss != updated.Epss) epssChanged++;

                    updated = updated with
                    {
                        Epss = newEpss,
                        Sources = updated.Sources.Select(src => src.Name == "EPSS"
                            ? src with { Status = found ? "ok" : "not_found", FetchedAt = now, Message = found ? null : "EPSS has not scored this CVE." }
                            : src).ToList(),
                    };
                }

                if (updated == doc) continue;
                VulnerabilityIntelService.Apply(row, updated, row.Status, row.FetchedAt, row.RefreshAfter);
            }

            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            if (rows.Count < PageSize) break;
        }

        if (kevChanged + epssChanged > 0)
            logger.LogInformation("Intel: signals refreshed — {Kev} KEV change(s), {Epss} EPSS update(s)", kevChanged, epssChanged);
        return (kevChanged, epssChanged);
    }
}
