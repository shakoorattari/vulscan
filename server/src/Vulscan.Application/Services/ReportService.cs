using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vulscan.Application.Common;
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
    /// <summary>What the stored intelligence says about a CVE, for the fields reports show next to each finding.</summary>
    private sealed record IntelFacts(double? Cvss, bool Kev, DateTime? KevDueDate, double? Epss, double? EpssPercentile);

    /// <summary>
    /// Real CVSS score, KEV listing and EPSS for the given CVEs, where intelligence has been gathered. The real score
    /// replaces the one stored with the finding, which older scans estimated from a few vector flags.
    /// </summary>
    private async Task<Dictionary<string, IntelFacts>> LoadIntelAsync(IEnumerable<string> cveIds, CancellationToken ct)
    {
        var facts = new Dictionary<string, IntelFacts>(StringComparer.OrdinalIgnoreCase);
        foreach (var batch in cveIds.Distinct(StringComparer.OrdinalIgnoreCase).Chunk(500))
        {
            var rows = await db.Set<VulnerabilityIntel>()
                .Where(i => batch.Contains(i.AdvisoryId) && i.Status != IntelStatus.NotFound)
                .Select(i => new { i.AdvisoryId, i.CvssScore, i.KevListed, i.KevDueDate, i.EpssScore, i.EpssPercentile })
                .ToListAsync(ct);
            foreach (var r in rows)
                facts[r.AdvisoryId] = new IntelFacts(r.CvssScore, r.KevListed, r.KevDueDate, r.EpssScore, r.EpssPercentile);
        }
        return facts;
    }

    public async Task<ExecutiveSummaryReportDto> GetExecutiveSummaryAsync(
        Guid? scanRunId = null, CancellationToken ct = default)
    {
        logger.LogInformation("Generating executive summary report (ScanRunId: {ScanRunId})", scanRunId?.ToString() ?? "current");

        // Default: each project's current (latest completed) scan, summed. History is untouched.
        var scanIds = await CurrentScans.ResolveAsync(db, scanRunId, ct);

        var shownScans = await db.Set<ScanRun>()
            .Where(s => scanIds.Contains(s.Id))
            .Select(s => new { s.CompletedAt, s.StartedAt, s.DurationSeconds })
            .ToListAsync(ct);
        var latest = shownScans.OrderByDescending(s => s.CompletedAt ?? s.StartedAt).FirstOrDefault();

        var projects = await db.Set<Project>().CountAsync(ct);
        var repos = await db.Set<Repository>().CountAsync(ct);
        var totalScans = await db.Set<ScanRun>().CountAsync(s => s.Status == ScanStatus.Completed, ct);
        var inProgress = (await CurrentScans.ProjectsInProgressAsync(db, ct)).Count;

        var packages = db.Set<DiscoveredPackage>().Where(p => scanIds.Contains(p.ScanRunId));
        var totalPackages = await packages.CountAsync(ct);

        var bySeverity = await db.Set<Vulnerability>()
            .Where(v => scanIds.Contains(v.ScanRunId))
            .GroupBy(v => v.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        int Of(VulnerabilitySeverity sev) => bySeverity.FirstOrDefault(x => x.Severity == sev)?.Count ?? 0;
        int critical = Of(VulnerabilitySeverity.Critical), high = Of(VulnerabilitySeverity.High),
            medium = Of(VulnerabilitySeverity.Medium), low = Of(VulnerabilitySeverity.Low);

        var ecosystemData = await packages
            .GroupBy(p => p.Ecosystem)
            .Select(g => new EcosystemBreakdownDto
            {
                Ecosystem = g.Key,
                TotalPackages = g.Count(),
                UniquePackages = g.Select(p => p.Name).Distinct().Count(),
                VulnerablePackages = g.Count(p => p.HasVulnerabilities)
            })
            .ToListAsync(ct);

        return new ExecutiveSummaryReportDto
        {
            TotalProjects = projects,
            TotalRepositories = repos,
            TotalScans = totalScans,
            TotalPackages = totalPackages,
            TotalVulnerabilities = critical + high + medium + low,
            CriticalCount = critical,
            HighCount = high,
            MediumCount = medium,
            LowCount = low,
            LastScanDate = latest?.CompletedAt ?? latest?.StartedAt,
            LastScanDurationSeconds = latest?.DurationSeconds,
            ScansInProgress = inProgress,
            EcosystemBreakdown = ecosystemData,
            ProjectSummaries = await GetProjectSummariesAsync(scanRunId, ct),
            SeverityTrend = await GetSeverityTrendsAsync(10, ct)
        };
    }

    public async Task<List<ProjectSummaryDto>> GetProjectSummariesAsync(
        Guid? scanRunId = null, CancellationToken ct = default)
    {
        logger.LogInformation("Generating project summaries (ScanRunId: {ScanRunId})", scanRunId?.ToString() ?? "current");

        var scanIds = await CurrentScans.ResolveAsync(db, scanRunId, ct);

        // Which project each shown scan belongs to, and when it finished.
        var scanOwner = await db.Set<ScanRun>()
            .Where(s => scanIds.Contains(s.Id))
            .Select(s => new { s.Id, s.ProjectId, At = s.CompletedAt ?? s.StartedAt })
            .ToListAsync(ct);
        var scanByProject = scanOwner.ToDictionary(s => s.ProjectId);
        var projectOfScan = scanOwner.ToDictionary(s => s.Id, s => s.ProjectId);

        var packageCounts = (await db.Set<DiscoveredPackage>()
                .Where(p => scanIds.Contains(p.ScanRunId))
                .GroupBy(p => p.ScanRunId)
                .Select(g => new { ScanRunId = g.Key, Count = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => x.ScanRunId, x => x.Count);

        var severityCounts = (await db.Set<Vulnerability>()
                .Where(v => scanIds.Contains(v.ScanRunId))
                .GroupBy(v => new { v.ScanRunId, v.Severity })
                .Select(g => new { g.Key.ScanRunId, g.Key.Severity, Count = g.Count() })
                .ToListAsync(ct))
            .ToLookup(x => x.ScanRunId);

        var inProgress = (await CurrentScans.ProjectsInProgressAsync(db, ct)).ToHashSet();

        var projects = await db.Set<Project>()
            .Select(p => new { p.Id, p.Name, RepositoryCount = p.Repositories.Count })
            .ToListAsync(ct);

        var result = new List<ProjectSummaryDto>();
        foreach (var project in projects)
        {
            scanByProject.TryGetValue(project.Id, out var scan);
            int Count(VulnerabilitySeverity sev) =>
                scan is null ? 0 : severityCounts[scan.Id].Where(x => x.Severity == sev).Sum(x => x.Count);

            int critical = Count(VulnerabilitySeverity.Critical), high = Count(VulnerabilitySeverity.High),
                medium = Count(VulnerabilitySeverity.Medium), low = Count(VulnerabilitySeverity.Low);

            result.Add(new ProjectSummaryDto
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                RepositoryCount = project.RepositoryCount,
                TotalPackages = scan is not null && packageCounts.TryGetValue(scan.Id, out var pkgs) ? pkgs : 0,
                TotalVulnerabilities = critical + high + medium + low,
                CriticalCount = critical,
                HighCount = high,
                MediumCount = medium,
                LowCount = low,
                LastScanAt = scan?.At,
                ScanInProgress = inProgress.Contains(project.Id)
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

        // The project's own current scan (its latest *completed* one) unless a specific scan was asked for. A scan
        // that is queued or running is not shown, so the report never blanks out while a new scan is in progress.
        var effectiveScanId = scanRunId ?? await CurrentScans.IdForProjectAsync(db, projectId, ct) ?? Guid.Empty;
        var lastScanAt = effectiveScanId == Guid.Empty
            ? null
            : await db.Set<ScanRun>()
                .Where(s => s.Id == effectiveScanId)
                .Select(s => (DateTime?)(s.CompletedAt ?? s.StartedAt))
                .FirstOrDefaultAsync(ct);
        var scanInProgress = await db.Set<ScanRun>()
            .AnyAsync(s => s.ProjectId == projectId && (s.Status == ScanStatus.Queued || s.Status == ScanStatus.Running), ct);

        var repoIds = project.Repositories.Select(r => r.Id).ToList();

        var packagesQuery = db.Set<DiscoveredPackage>().Where(p => repoIds.Contains(p.RepositoryId) && p.ScanRunId == effectiveScanId);
        var vulnsQuery = db.Set<Vulnerability>().Where(v => repoIds.Contains(v.RepositoryId) && v.ScanRunId == effectiveScanId);

        var allPackages = await packagesQuery.ToListAsync(ct);
        var allVulns = await vulnsQuery.Include(v => v.Repository).ToListAsync(ct);
        var intel = await LoadIntelAsync(allVulns.Select(v => v.CveId), ct);

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

            // The same package version is often declared in several manifests (and so recorded once per
            // manifest). Collapse those into one finding and keep where it occurs.
            var packagesByVersion = repoPackages.ToLookup(p => (p.Name, p.Version));

            var findings = repoVulns
                .GroupBy(v => (v.CveId, v.PackageName, v.InstalledVersion))
                .Select(g =>
                {
                    var v = g.OrderByDescending(x => x.CvssScore ?? 0).First();
                    var declared = packagesByVersion[(v.PackageName, v.InstalledVersion)].ToList();
                    intel.TryGetValue(v.CveId, out var facts);
                    return new ReportVulnerabilityDto
                    {
                        Id = v.Id,
                        CveId = v.CveId,
                        PackageName = v.PackageName,
                        InstalledVersion = v.InstalledVersion,
                        FixedVersion = g.Select(x => x.FixedVersion).FirstOrDefault(f => !string.IsNullOrEmpty(f)),
                        Severity = v.Severity.ToString(),
                        CvssScore = facts?.Cvss ?? v.CvssScore,
                        Description = v.Description,
                        Status = v.Status.ToString(),
                        FirstDetectedAt = g.Min(x => x.FirstDetectedAt),
                        AgeDays = (int)(DateTime.UtcNow - g.Min(x => x.FirstDetectedAt)).TotalDays,
                        Ecosystem = declared.Select(p => p.Ecosystem).FirstOrDefault(e => !string.IsNullOrEmpty(e)),
                        Occurrences = g.Count(),
                        Kev = facts?.Kev ?? false,
                        KevDueDate = facts?.KevDueDate,
                        Epss = facts?.Epss,
                        EpssPercentile = facts?.EpssPercentile,
                        SourceFiles = declared.Select(p => p.SourceFile)
                            .Where(f => !string.IsNullOrEmpty(f))
                            .Distinct()
                            .Order()
                            .ToList()
                    };
                })
                .OrderByDescending(v => v.CvssScore ?? 0)
                .ThenBy(v => v.CveId, StringComparer.Ordinal)
                .ToList();

            return new RepositoryReportDto
            {
                RepositoryId = repo.Id,
                RepositoryName = repo.Name,
                TotalPackages = repoPackages.Count,
                VulnerablePackages = repoPackages.Count(p => p.HasVulnerabilities),
                Vulnerabilities = findings,
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
            LastScanAt = lastScanAt,
            ScanInProgress = scanInProgress,
            TotalRepositories = project.Repositories.Count,
            TotalPackages = allPackages.Count,
            TotalVulnerabilities = allVulns.Count,
            UniqueVulnerabilities = repos.Sum(r => r.Vulnerabilities.Count),
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
            severity ?? "all", scanRunId?.ToString() ?? "current");

        var scanIds = await CurrentScans.ResolveAsync(db, scanRunId, ct);
        var vulnsQuery = db.Set<Vulnerability>().Where(v => scanIds.Contains(v.ScanRunId));

        if (!string.IsNullOrEmpty(severity) &&
            Enum.TryParse<VulnerabilitySeverity>(severity, true, out var severityEnum))
        {
            vulnsQuery = vulnsQuery.Where(v => v.Severity == severityEnum);
        }

        var vulns = await vulnsQuery.Include(v => v.Repository).ToListAsync(ct);
        var intel = await LoadIntelAsync(vulns.Select(v => v.CveId), ct);

        // Group by CVE
        var grouped = vulns
            .GroupBy(v => v.CveId)
            .Select(g =>
            {
                var first = g.First();
                intel.TryGetValue(g.Key, out var facts);
                return new VulnerabilitySummaryDto
                {
                    CveId = g.Key,
                    Severity = first.Severity.ToString(),
                    CvssScore = facts?.Cvss ?? first.CvssScore,
                    Kev = facts?.Kev ?? false,
                    Epss = facts?.Epss,
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

        var scanIds = await CurrentScans.ResolveAsync(db, scanRunId, ct);
        var vulnsQuery = db.Set<Vulnerability>().Where(v => v.CveId == cveId && scanIds.Contains(v.ScanRunId));

        var vulns = await vulnsQuery
            .Include(v => v.Repository)
                .ThenInclude(r => r.Project)
            .ToListAsync(ct);

        if (vulns.Count == 0)
            return null;

        var first = vulns.First();
        var facts = (await LoadIntelAsync([cveId], ct)).GetValueOrDefault(cveId);

        return new VulnerabilityDetailReportDto
        {
            CveId = cveId,
            Severity = first.Severity.ToString(),
            CvssScore = facts?.Cvss ?? first.CvssScore,
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

    /// <summary>
    /// How the estate's totals changed over time, built from the full scan history. Each point is a completed scan;
    /// its figures are the sum of every project's most recent completed scan <i>as of that moment</i>. (Plotting raw
    /// scans would alternate between projects and read as swings that never happened.)
    /// </summary>
    public async Task<List<SeverityTrendDto>> GetSeverityTrendsAsync(int count = 10, CancellationToken ct = default)
    {
        var scans = await db.Set<ScanRun>()
            .Where(s => s.Status == ScanStatus.Completed)
            .OrderBy(s => s.CompletedAt).ThenBy(s => s.Id)
            .Select(s => new
            {
                s.Id, s.ProjectId, At = s.CompletedAt ?? s.StartedAt,
                s.CriticalCount, s.HighCount, s.MediumCount, s.LowCount
            })
            .ToListAsync(ct);

        var latestByProject = new Dictionary<Guid, int>();   // project -> index into scans
        var points = new List<SeverityTrendDto>(scans.Count);
        for (var i = 0; i < scans.Count; i++)
        {
            latestByProject[scans[i].ProjectId] = i;
            int critical = 0, high = 0, medium = 0, low = 0;
            foreach (var idx in latestByProject.Values)
            {
                critical += scans[idx].CriticalCount;
                high += scans[idx].HighCount;
                medium += scans[idx].MediumCount;
                low += scans[idx].LowCount;
            }

            points.Add(new SeverityTrendDto
            {
                ScanDate = scans[i].At,
                ScanId = scans[i].Id,
                Critical = critical,
                High = high,
                Medium = medium,
                Low = low,
                Total = critical + high + medium + low
            });
        }

        return points.Skip(Math.Max(0, points.Count - count)).ToList();
    }
}
