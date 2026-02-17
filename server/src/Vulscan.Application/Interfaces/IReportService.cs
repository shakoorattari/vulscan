using Vulscan.Application.DTOs.Reports;

namespace Vulscan.Application.Interfaces;

/// <summary>
/// Service for generating project-level, vulnerability-level, and executive summary reports.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Generate an executive summary report covering all scans.
    /// </summary>
    Task<ExecutiveSummaryReportDto> GetExecutiveSummaryAsync(int? scanRunId = null, CancellationToken ct = default);

    /// <summary>
    /// Get summary list of all projects with vulnerability counts.
    /// </summary>
    Task<List<ProjectSummaryDto>> GetProjectSummariesAsync(int? scanRunId = null, CancellationToken ct = default);

    /// <summary>
    /// Get detailed report for a specific project (repos, packages, vulns).
    /// </summary>
    Task<ProjectDetailReportDto?> GetProjectReportAsync(int projectId, int? scanRunId = null, CancellationToken ct = default);

    /// <summary>
    /// Get summary list of all unique CVEs with affected repo counts.
    /// </summary>
    Task<List<VulnerabilitySummaryDto>> GetVulnerabilitySummariesAsync(
        string? severity = null, int? scanRunId = null, CancellationToken ct = default);

    /// <summary>
    /// Get detailed report for a specific CVE across all repos.
    /// </summary>
    Task<VulnerabilityDetailReportDto?> GetVulnerabilityReportAsync(string cveId, int? scanRunId = null, CancellationToken ct = default);

    /// <summary>
    /// Get severity trend data across recent scans for chart visualization.
    /// </summary>
    Task<List<SeverityTrendDto>> GetSeverityTrendsAsync(int count = 10, CancellationToken ct = default);
}
