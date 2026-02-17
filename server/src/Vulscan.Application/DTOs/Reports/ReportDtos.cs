namespace Vulscan.Application.DTOs.Reports;

/// <summary>
/// Executive summary report with overall scan statistics.
/// </summary>
public sealed record ExecutiveSummaryReportDto
{
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public int TotalProjects { get; init; }
    public int TotalRepositories { get; init; }
    public int TotalScans { get; init; }
    public int TotalPackages { get; init; }
    public int TotalVulnerabilities { get; init; }
    public int CriticalCount { get; init; }
    public int HighCount { get; init; }
    public int MediumCount { get; init; }
    public int LowCount { get; init; }
    public DateTime? LastScanDate { get; init; }
    public int? LastScanDurationSeconds { get; init; }
    public List<EcosystemBreakdownDto> EcosystemBreakdown { get; init; } = [];
    public List<ProjectSummaryDto> ProjectSummaries { get; init; } = [];
    public List<SeverityTrendDto> SeverityTrend { get; init; } = [];
}

/// <summary>
/// Breakdown by ecosystem (npm, nuget, etc.).
/// </summary>
public sealed record EcosystemBreakdownDto
{
    public string Ecosystem { get; init; } = string.Empty;
    public int TotalPackages { get; init; }
    public int UniquePackages { get; init; }
    public int VulnerablePackages { get; init; }
}

/// <summary>
/// Summary of a project with vulnerability counts.
/// </summary>
public sealed record ProjectSummaryDto
{
    public int ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public int RepositoryCount { get; init; }
    public int TotalPackages { get; init; }
    public int TotalVulnerabilities { get; init; }
    public int CriticalCount { get; init; }
    public int HighCount { get; init; }
    public int MediumCount { get; init; }
    public int LowCount { get; init; }
}

/// <summary>
/// Detailed report for a single project including all repos, packages, and vulnerabilities.
/// </summary>
public sealed record ProjectDetailReportDto
{
    public int ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public int TotalRepositories { get; init; }
    public int TotalPackages { get; init; }
    public int TotalVulnerabilities { get; init; }
    public int CriticalCount { get; init; }
    public int HighCount { get; init; }
    public int MediumCount { get; init; }
    public int LowCount { get; init; }
    public List<EcosystemBreakdownDto> EcosystemBreakdown { get; init; } = [];
    public List<RepositoryReportDto> Repositories { get; init; } = [];
}

/// <summary>
/// Repository-level report within a project.
/// </summary>
public sealed record RepositoryReportDto
{
    public int RepositoryId { get; init; }
    public string RepositoryName { get; init; } = string.Empty;
    public int TotalPackages { get; init; }
    public int VulnerablePackages { get; init; }
    public List<ReportVulnerabilityDto> Vulnerabilities { get; init; } = [];
    public List<ReportPackageDto> TopPackages { get; init; } = [];
}

/// <summary>
/// Vulnerability item in a report.
/// </summary>
public sealed record ReportVulnerabilityDto
{
    public int Id { get; init; }
    public string CveId { get; init; } = string.Empty;
    public string PackageName { get; init; } = string.Empty;
    public string InstalledVersion { get; init; } = string.Empty;
    public string? FixedVersion { get; init; }
    public string Severity { get; init; } = string.Empty;
    public double? CvssScore { get; init; }
    public string? Description { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime FirstDetectedAt { get; init; }
    public int AgeDays { get; init; }
}

/// <summary>
/// Package item in a report.
/// </summary>
public sealed record ReportPackageDto
{
    public string Ecosystem { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string? SourceFile { get; init; }
    public bool HasVulnerabilities { get; init; }
    public string? Purl { get; init; }
}

/// <summary>
/// Vulnerability detail report showing all repos/packages affected by a specific CVE.
/// </summary>
public sealed record VulnerabilityDetailReportDto
{
    public string CveId { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public double? CvssScore { get; init; }
    public string? Description { get; init; }
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public int AffectedRepositories { get; init; }
    public int AffectedProjects { get; init; }
    public int TotalOccurrences { get; init; }
    public List<AffectedRepositoryDto> Repositories { get; init; } = [];
}

/// <summary>
/// Repository affected by a vulnerability.
/// </summary>
public sealed record AffectedRepositoryDto
{
    public int RepositoryId { get; init; }
    public string RepositoryName { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string PackageName { get; init; } = string.Empty;
    public string InstalledVersion { get; init; } = string.Empty;
    public string? FixedVersion { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime FirstDetectedAt { get; init; }
}

/// <summary>
/// Summary of a CVE across the codebase.
/// </summary>
public sealed record VulnerabilitySummaryDto
{
    public string CveId { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public double? CvssScore { get; init; }
    public string PackageName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int AffectedRepositories { get; init; }
    public int TotalOccurrences { get; init; }
    public string? FixedVersion { get; init; }
}

/// <summary>
/// Severity trend data point for charts.
/// </summary>
public sealed record SeverityTrendDto
{
    public DateTime ScanDate { get; init; }
    public int ScanId { get; init; }
    public int Critical { get; init; }
    public int High { get; init; }
    public int Medium { get; init; }
    public int Low { get; init; }
    public int Total { get; init; }
}
