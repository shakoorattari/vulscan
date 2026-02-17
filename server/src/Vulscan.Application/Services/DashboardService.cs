using Microsoft.EntityFrameworkCore;
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

        var vulnCounts = await dbContext.Set<Vulnerability>()
            .GroupBy(v => v.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var lastScan = await dbContext.Set<ScanRun>()
            .OrderByDescending(s => s.StartedAt)
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
            LastScanDate = lastScan?.StartedAt,
            LastScanStatus = lastScan?.Status.ToString(),
            RecentScans = recentScans,
            TopVulnerableRepos = topVulnerableRepos
        };
    }
}
