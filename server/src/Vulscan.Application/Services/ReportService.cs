using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vulscan.Application.DTOs.Reports;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;

namespace Vulscan.Application.Services;

/// <summary>
/// Generates project-level, vulnerability-level, and executive summary reports.
/// </summary>
public sealed class ReportService(DbContext db, ILogger<ReportService> logger) : IReportService
{
    public async Task<ExecutiveSummaryReportDto> GetExecutiveSummaryAsync(
        Guid? scanRunId = null, CancellationToken ct = default)
    {
        logger.LogInformation("Generating executive summary report (ScanRunId: {ScanRunId})", scanRunId?.ToString() ?? "latest");

        // Determine the latest scan if no specific one requested
        Guid effectiveScanId;
        if (scanRunId.HasValue)
        {
            effectiveScanId = scanRunId.Value;
        }
        else
        {
            var latestScan = await db.Set<ScanRun>()
                .Where(s => s.Status == ScanStatus.Completed)
                .OrderByDescending(s => s.CompletedAt)
                .FirstOrDefaultAsync(ct);
            effectiveScanId = latestScan?.Id ?? Guid.Empty;
        }

        var scanRun = await db.Set<ScanRun>().FirstOrDefaultAsync(s => s.Id == effectiveScanId, ct);

        // Projects
        var projects = await db.Set<Project>().CountAsync(ct);
        var repos = await db.Set<Repository>().CountAsync(ct);
        var totalScans = await db.Set<ScanRun>().CountAsync(s => s.Status == ScanStatus.Completed, ct);

        // Packages for the scan
        var packagesQuery = effectiveScanId != Guid.Empty
            ? db.Set<DiscoveredPackage>().Where(p => p.ScanRunId == effectiveScanId)
            : db.Set<DiscoveredPackage>();
        var totalPackages = await packagesQuery.CountAsync(ct);

        // Vulnerabilities for the scan
        var vulnsQuery = effectiveScanId != Guid.Empty
            ? db.Set<Vulnerability>().Where(v => v.ScanRunId == effectiveScanId)
            : db.Set<Vulnerability>();

        var criticalCount = await vulnsQuery.CountAsync(v => v.Severity == VulnerabilitySeverity.Critical, ct);
        var highCount = await vulnsQuery.CountAsync(v => v.Severity == VulnerabilitySeverity.High, ct);
        var mediumCount = await vulnsQuery.CountAsync(v => v.Severity == VulnerabilitySeverity.Medium, ct);
        var lowCount = await vulnsQuery.CountAsync(v => v.Severity == VulnerabilitySeverity.Low, ct);

        // Ecosystem breakdown
        var ecosystemData = await packagesQuery
            .GroupBy(p => p.Ecosystem)
            .Select(g => new EcosystemBreakdownDto
            {
                Ecosystem = g.Key,
                TotalPackages = g.Count(),
                UniquePackages = g.Select(p => p.Name).Distinct().Count(),
                VulnerablePackages = g.Count(p => p.HasVulnerabilities)
            })
            .ToListAsync(ct);

        // Project summaries
        var projectSummaries = await GetProjectSummariesAsync(scanRunId, ct);

        // Severity trends
        var trends = await GetSeverityTrendsAsync(10, ct);

        return new ExecutiveSummaryReportDto
        {
            TotalProjects = projects,
            TotalRepositories = repos,
            TotalScans = totalScans,
            TotalPackages = totalPackages,
            TotalVulnerabilities = criticalCount + highCount + mediumCount + lowCount,
            CriticalCount = criticalCount,
            HighCount = highCount,
            MediumCount = mediumCount,
            LowCount = lowCount,
            LastScanDate = scanRun?.CompletedAt ?? scanRun?.StartedAt,
            LastScanDurationSeconds = scanRun?.DurationSeconds,
            EcosystemBreakdown = ecosystemData,
            ProjectSummaries = projectSummaries,
            SeverityTrend = trends
        };
    }

    public async Task<List<ProjectSummaryDto>> GetProjectSummariesAsync(
        Guid? scanRunId = null, CancellationToken ct = default)
    {
        logger.LogInformation("Generating project summaries (ScanRunId: {ScanRunId})", scanRunId?.ToString() ?? "latest");

        // Get latest scan if not specified
        Guid effectiveScanId;
        if (scanRunId.HasValue)
        {
            effectiveScanId = scanRunId.Value;
        }
        else
        {
            var latestScan = await db.Set<ScanRun>()
                .Where(s => s.Status == ScanStatus.Completed)
                .OrderByDescending(s => s.CompletedAt)
                .FirstOrDefaultAsync(ct);
            effectiveScanId = latestScan?.Id ?? Guid.Empty;
        }

        var projects = await db.Set<Project>()
            .Include(p => p.Repositories)
            .ToListAsync(ct);

        var result = new List<ProjectSummaryDto>();

        foreach (var project in projects)
        {
            var repoIds = project.Repositories.Select(r => r.Id).ToList();

            var packagesQuery = effectiveScanId != Guid.Empty
                ? db.Set<DiscoveredPackage>().Where(p => repoIds.Contains(p.RepositoryId) && p.ScanRunId == effectiveScanId)
                : db.Set<DiscoveredPackage>().Where(p => repoIds.Contains(p.RepositoryId));

            var vulnsQuery = effectiveScanId != Guid.Empty
                ? db.Set<Vulnerability>().Where(v => repoIds.Contains(v.RepositoryId) && v.ScanRunId == effectiveScanId)
                : db.Set<Vulnerability>().Where(v => repoIds.Contains(v.RepositoryId));

            var totalPkgs = await packagesQuery.CountAsync(ct);
            var critical = await vulnsQuery.CountAsync(v => v.Severity == VulnerabilitySeverity.Critical, ct);
            var high = await vulnsQuery.CountAsync(v => v.Severity == VulnerabilitySeverity.High, ct);
            var medium = await vulnsQuery.CountAsync(v => v.Severity == VulnerabilitySeverity.Medium, ct);
            var low = await vulnsQuery.CountAsync(v => v.Severity == VulnerabilitySeverity.Low, ct);

            result.Add(new ProjectSummaryDto
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                RepositoryCount = project.Repositories.Count,
                TotalPackages = totalPkgs,
                TotalVulnerabilities = critical + high + medium + low,
                CriticalCount = critical,
                HighCount = high,
                MediumCount = medium,
                LowCount = low
            });
        }

        return result.OrderByDescending(p => p.TotalVulnerabilities)
            .ThenByDescending(p => p.CriticalCount)
            .ToList();
    }

    public async Task<ProjectDetailReportDto?> GetProjectReportAsync(
        Guid projectId, Guid? scanRunId = null, CancellationToken ct = default)
    {
        logger.LogInformation("Generating project report for ProjectId: {ProjectId}", projectId);

        var project = await db.Set<Project>()
            .Include(p => p.Repositories)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project is null)
            return null;

        // Get latest scan if not specified
        Guid effectiveScanId;
        if (scanRunId.HasValue)
        {
            effectiveScanId = scanRunId.Value;
        }
        else
        {
            var latestScan = await db.Set<ScanRun>()
                .Where(s => s.Status == ScanStatus.Completed)
                .OrderByDescending(s => s.CompletedAt)
                .FirstOrDefaultAsync(ct);
            effectiveScanId = latestScan?.Id ?? Guid.Empty;
        }

        var repoIds = project.Repositories.Select(r => r.Id).ToList();

        var packagesQuery = effectiveScanId != Guid.Empty
            ? db.Set<DiscoveredPackage>().Where(p => repoIds.Contains(p.RepositoryId) && p.ScanRunId == effectiveScanId)
            : db.Set<DiscoveredPackage>().Where(p => repoIds.Contains(p.RepositoryId));

        var vulnsQuery = effectiveScanId != Guid.Empty
            ? db.Set<Vulnerability>().Where(v => repoIds.Contains(v.RepositoryId) && v.ScanRunId == effectiveScanId)
            : db.Set<Vulnerability>().Where(v => repoIds.Contains(v.RepositoryId));

        var allPackages = await packagesQuery.ToListAsync(ct);
        var allVulns = await vulnsQuery.Include(v => v.Repository).ToListAsync(ct);

        // Ecosystem breakdown
        var ecosystemBreakdown = allPackages
            .GroupBy(p => p.Ecosystem)
            .Select(g => new EcosystemBreakdownDto
            {
                Ecosystem = g.Key,
                TotalPackages = g.Count(),
                UniquePackages = g.Select(p => p.Name).Distinct().Count(),
                VulnerablePackages = g.Count(p => p.HasVulnerabilities)
            })
            .ToList();

        // Repos
        var repos = project.Repositories.Select(repo =>
        {
            var repoPackages = allPackages.Where(p => p.RepositoryId == repo.Id).ToList();
            var repoVulns = allVulns.Where(v => v.RepositoryId == repo.Id).ToList();

            return new RepositoryReportDto
            {
                RepositoryId = repo.Id,
                RepositoryName = repo.Name,
                TotalPackages = repoPackages.Count,
                VulnerablePackages = repoPackages.Count(p => p.HasVulnerabilities),
                Vulnerabilities = repoVulns.Select(v => new ReportVulnerabilityDto
                {
                    Id = v.Id,
                    CveId = v.CveId,
                    PackageName = v.PackageName,
                    InstalledVersion = v.InstalledVersion,
                    FixedVersion = v.FixedVersion,
                    Severity = v.Severity.ToString(),
                    CvssScore = v.CvssScore,
                    Description = v.Description,
                    Status = v.Status.ToString(),
                    FirstDetectedAt = v.FirstDetectedAt,
                    AgeDays = (int)(DateTime.UtcNow - v.FirstDetectedAt).TotalDays
                }).OrderByDescending(v => v.CvssScore ?? 0).ToList(),
                TopPackages = repoPackages
                    .OrderByDescending(p => p.HasVulnerabilities)
                    .ThenBy(p => p.Name)
                    .Take(50)
                    .Select(p => new ReportPackageDto
                    {
                        Ecosystem = p.Ecosystem,
                        Name = p.Name,
                        Version = p.Version,
                        SourceFile = p.SourceFile,
                        HasVulnerabilities = p.HasVulnerabilities,
                        Purl = p.Purl
                    }).ToList()
            };
        })
        .OrderByDescending(r => r.Vulnerabilities.Count)
        .ToList();

        return new ProjectDetailReportDto
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            TotalRepositories = project.Repositories.Count,
            TotalPackages = allPackages.Count,
            TotalVulnerabilities = allVulns.Count,
            CriticalCount = allVulns.Count(v => v.Severity == VulnerabilitySeverity.Critical),
            HighCount = allVulns.Count(v => v.Severity == VulnerabilitySeverity.High),
            MediumCount = allVulns.Count(v => v.Severity == VulnerabilitySeverity.Medium),
            LowCount = allVulns.Count(v => v.Severity == VulnerabilitySeverity.Low),
            EcosystemBreakdown = ecosystemBreakdown,
            Repositories = repos
        };
    }

    public async Task<List<VulnerabilitySummaryDto>> GetVulnerabilitySummariesAsync(
        string? severity = null, Guid? scanRunId = null, CancellationToken ct = default)
    {
        logger.LogInformation("Generating vulnerability summaries (Severity: {Severity}, ScanRunId: {ScanRunId})",
            severity ?? "all", scanRunId?.ToString() ?? "latest");

        // Get latest scan if not specified
        Guid effectiveScanId;
        if (scanRunId.HasValue)
        {
            effectiveScanId = scanRunId.Value;
        }
        else
        {
            var latestScan = await db.Set<ScanRun>()
                .Where(s => s.Status == ScanStatus.Completed)
                .OrderByDescending(s => s.CompletedAt)
                .FirstOrDefaultAsync(ct);
            effectiveScanId = latestScan?.Id ?? Guid.Empty;
        }

        var vulnsQuery = effectiveScanId != Guid.Empty
            ? db.Set<Vulnerability>().Where(v => v.ScanRunId == effectiveScanId)
            : db.Set<Vulnerability>().AsQueryable();

        if (!string.IsNullOrEmpty(severity) &&
            Enum.TryParse<VulnerabilitySeverity>(severity, true, out var severityEnum))
        {
            vulnsQuery = vulnsQuery.Where(v => v.Severity == severityEnum);
        }

        var vulns = await vulnsQuery.Include(v => v.Repository).ToListAsync(ct);

        // Group by CVE
        var grouped = vulns
            .GroupBy(v => v.CveId)
            .Select(g =>
            {
                var first = g.First();
                return new VulnerabilitySummaryDto
                {
                    CveId = g.Key,
                    Severity = first.Severity.ToString(),
                    CvssScore = first.CvssScore,
                    PackageName = first.PackageName,
                    Description = first.Description,
                    AffectedRepositories = g.Select(v => v.RepositoryId).Distinct().Count(),
                    TotalOccurrences = g.Count(),
                    FixedVersion = first.FixedVersion
                };
            })
            .OrderByDescending(v => v.CvssScore ?? 0)
            .ThenByDescending(v => v.AffectedRepositories)
            .ToList();

        return grouped;
    }

    public async Task<VulnerabilityDetailReportDto?> GetVulnerabilityReportAsync(
        string cveId, Guid? scanRunId = null, CancellationToken ct = default)
    {
        logger.LogInformation("Generating vulnerability detail report for CVE: {CveId}", cveId);

        // Get latest scan if not specified
        Guid effectiveScanId;
        if (scanRunId.HasValue)
        {
            effectiveScanId = scanRunId.Value;
        }
        else
        {
            var latestScan = await db.Set<ScanRun>()
                .Where(s => s.Status == ScanStatus.Completed)
                .OrderByDescending(s => s.CompletedAt)
                .FirstOrDefaultAsync(ct);
            effectiveScanId = latestScan?.Id ?? Guid.Empty;
        }

        var vulnsQuery = effectiveScanId != Guid.Empty
            ? db.Set<Vulnerability>().Where(v => v.CveId == cveId && v.ScanRunId == effectiveScanId)
            : db.Set<Vulnerability>().Where(v => v.CveId == cveId);

        var vulns = await vulnsQuery
            .Include(v => v.Repository)
                .ThenInclude(r => r.Project)
            .ToListAsync(ct);

        if (vulns.Count == 0)
            return null;

        var first = vulns.First();

        return new VulnerabilityDetailReportDto
        {
            CveId = cveId,
            Severity = first.Severity.ToString(),
            CvssScore = first.CvssScore,
            Description = first.Description,
            AffectedRepositories = vulns.Select(v => v.RepositoryId).Distinct().Count(),
            AffectedProjects = vulns.Select(v => v.Repository?.ProjectId).Distinct().Count(),
            TotalOccurrences = vulns.Count,
            Repositories = vulns.Select(v => new AffectedRepositoryDto
            {
                RepositoryId = v.RepositoryId,
                RepositoryName = v.Repository?.Name ?? "Unknown",
                ProjectName = v.Repository?.Project?.Name ?? "Unknown",
                PackageName = v.PackageName,
                InstalledVersion = v.InstalledVersion,
                FixedVersion = v.FixedVersion,
                Status = v.Status.ToString(),
                FirstDetectedAt = v.FirstDetectedAt
            })
            .OrderBy(r => r.ProjectName)
            .ThenBy(r => r.RepositoryName)
            .ToList()
        };
    }

    public async Task<List<SeverityTrendDto>> GetSeverityTrendsAsync(int count = 10, CancellationToken ct = default)
    {
        var scans = await db.Set<ScanRun>()
            .Where(s => s.Status == ScanStatus.Completed)
            .OrderByDescending(s => s.CompletedAt)
            .Take(count)
            .OrderBy(s => s.CompletedAt) // Ascending for chart
            .Select(s => new SeverityTrendDto
            {
                ScanDate = s.CompletedAt ?? s.StartedAt,
                ScanId = s.Id,
                Critical = s.CriticalCount,
                High = s.HighCount,
                Medium = s.MediumCount,
                Low = s.LowCount,
                Total = s.TotalVulnerabilities
            })
            .ToListAsync(ct);

        return scans;
    }
}
