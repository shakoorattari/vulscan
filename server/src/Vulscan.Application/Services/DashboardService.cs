using Microsoft.EntityFrameworkCore;
using Vulscan.Application.Common;
using Vulscan.Application.DTOs.Dashboard;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;

namespace Vulscan.Application.Services;

public sealed class DashboardService(DbContext dbContext) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var totalRepos = await dbContext.Set<Repository>().CountAsync(ct);
        var totalScans = await dbContext.Set<ScanRun>().CountAsync(ct);

        // Every scan is kept, but the figures describe each project's current scan (its latest completed one).
        // Summing all scans would count the same finding once per scan, and a scan that is still running — or one
        // that failed — must not replace the last good result with zeros.
        var currentScanIds = await CurrentScans.IdsAsync(dbContext, ct);

        var vulnCounts = await dbContext.Set<Vulnerability>()
            .Where(v => currentScanIds.Contains(v.ScanRunId))
            .GroupBy(v => v.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var resultsAsOf = currentScanIds.Count == 0
            ? null
            : await dbContext.Set<ScanRun>()
                .Where(s => currentScanIds.Contains(s.Id))
                .MaxAsync(s => s.CompletedAt, ct);

        var scansInProgress = (await CurrentScans.ProjectsInProgressAsync(dbContext, ct)).Count;

        // "Last scan" is the most recent one that has finished, so a scan in progress doesn't replace it.
        var lastFinished = await dbContext.Set<ScanRun>()
            .Where(s => s.Status != ScanStatus.Queued && s.Status != ScanStatus.Running)
            .OrderByDescending(s => s.CompletedAt ?? s.StartedAt)
            .Select(s => new { Date = s.CompletedAt ?? s.StartedAt, s.Status })
            .FirstOrDefaultAsync(ct);

        var recentScans = await dbContext.Set<ScanRun>()
            .OrderByDescending(s => s.StartedAt)
            .Take(10)
            .Select(s => new RecentScanDto
            {
                Id = s.Id,
                StartedAt = s.StartedAt,
                CompletedAt = s.CompletedAt,
                Status = s.Status.ToString(),
                ReposScanned = s.ReposScanned,
                TotalVulnerabilities = s.TotalVulnerabilities,
                TriggeredBy = s.TriggeredBy != null ? s.TriggeredBy.Username : "System"
            })
            .ToListAsync(ct);

        var topVulnerableRepos = await dbContext.Set<Vulnerability>()
            .Where(v => currentScanIds.Contains(v.ScanRunId))
            .Where(v => v.Status != VulnerabilityStatus.Resolved && v.Status != VulnerabilityStatus.Suppressed)
            .GroupBy(v => new { v.RepositoryId, v.Repository.Name, ProjectName = v.Repository.Project.Name })
            .Select(g => new TopVulnerableRepoDto
            {
                RepositoryId = g.Key.RepositoryId,
                RepositoryName = g.Key.Name,
                ProjectName = g.Key.ProjectName,
                CriticalCount = g.Count(v => v.Severity == VulnerabilitySeverity.Critical),
                HighCount = g.Count(v => v.Severity == VulnerabilitySeverity.High),
                TotalVulnerabilities = g.Count()
            })
            .OrderByDescending(r => r.CriticalCount)
            .ThenByDescending(r => r.HighCount)
            .Take(10)
            .ToListAsync(ct);

        return new DashboardSummaryDto
        {
            TotalRepositories = totalRepos,
            TotalScans = totalScans,
            TotalVulnerabilities = vulnCounts.Sum(v => v.Count),
            CriticalCount = vulnCounts.FirstOrDefault(v => v.Severity == VulnerabilitySeverity.Critical)?.Count ?? 0,
            HighCount = vulnCounts.FirstOrDefault(v => v.Severity == VulnerabilitySeverity.High)?.Count ?? 0,
            MediumCount = vulnCounts.FirstOrDefault(v => v.Severity == VulnerabilitySeverity.Medium)?.Count ?? 0,
            LowCount = vulnCounts.FirstOrDefault(v => v.Severity == VulnerabilitySeverity.Low)?.Count ?? 0,
            NegligibleCount = vulnCounts.FirstOrDefault(v => v.Severity == VulnerabilitySeverity.Negligible)?.Count ?? 0,
            LastScanDate = lastFinished?.Date,
            LastScanStatus = lastFinished?.Status.ToString(),
            ResultsAsOf = resultsAsOf,
            ScansInProgress = scansInProgress,
            RecentScans = recentScans,
            TopVulnerableRepos = topVulnerableRepos
        };
    }
}
