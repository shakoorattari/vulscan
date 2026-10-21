using Microsoft.EntityFrameworkCore;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;

namespace Vulscan.Application.Common;

/// <summary>
/// Which scan a dashboard or report should show.
///
/// Every scan is kept (history). What the UI presents by default is each project's <b>current</b> scan: its most
/// recent <i>completed</i> scan. A scan that is queued, running, failed or cancelled never replaces it, so results do
/// not blank out (or flash "0 vulnerabilities") while a new scan is in progress, and a failed run does not erase the
/// last good result. Scans belong to one project, so "latest" is always decided per project — never across projects.
/// </summary>
public static class CurrentScans
{
    /// <summary>Id of the current scan of every project that has at least one completed scan.</summary>
    public static Task<List<Guid>> IdsAsync(DbContext db, CancellationToken ct = default) =>
        db.Set<ScanRun>()
            .Where(s => s.Status == ScanStatus.Completed)
            .GroupBy(s => s.ProjectId)
            .Select(g => g
                .OrderByDescending(s => s.CompletedAt)
                .ThenByDescending(s => s.StartedAt)
                .ThenByDescending(s => s.Id)
                .Select(s => s.Id)
                .First())
            .ToListAsync(ct);

    /// <summary>Id of one project's current scan, or null if it has never completed a scan.</summary>
    public static async Task<Guid?> IdForProjectAsync(DbContext db, Guid projectId, CancellationToken ct = default) =>
        await db.Set<ScanRun>()
            .Where(s => s.ProjectId == projectId && s.Status == ScanStatus.Completed)
            .OrderByDescending(s => s.CompletedAt)
            .ThenByDescending(s => s.StartedAt)
            .ThenByDescending(s => s.Id)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>The scan to report on: the one asked for, or else every project's current scan.</summary>
    public static async Task<List<Guid>> ResolveAsync(DbContext db, Guid? scanRunId, CancellationToken ct = default) =>
        scanRunId.HasValue ? [scanRunId.Value] : await IdsAsync(db, ct);

    /// <summary>Projects that have a scan queued or running right now.</summary>
    public static Task<List<Guid>> ProjectsInProgressAsync(DbContext db, CancellationToken ct = default) =>
        db.Set<ScanRun>()
            .Where(s => s.Status == ScanStatus.Queued || s.Status == ScanStatus.Running)
            .Select(s => s.ProjectId)
            .Distinct()
            .ToListAsync(ct);
}
