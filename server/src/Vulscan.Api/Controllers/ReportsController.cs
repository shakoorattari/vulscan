using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Reports;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

/// <summary>
/// Reports API — executive summaries, per-project reports, and per-vulnerability reports.
/// Supports JSON, CSV, and downloadable exports.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public sealed class ReportsController(IReportService reportService, ILogger<ReportsController> logger) : ControllerBase
{
    // ── Executive Summary ────────────────────────────────────────────

    /// <summary>
    /// Generate an executive summary report with overall scan statistics,
    /// ecosystem breakdowns, project summaries, and severity trends.
    /// </summary>
    [HttpGet("executive-summary")]
    [ProducesResponseType(typeof(ApiResponse<ExecutiveSummaryReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExecutiveSummary([FromQuery] Guid? scanRunId, CancellationToken ct)
    {
        var report = await reportService.GetExecutiveSummaryAsync(scanRunId, ct);
        return Ok(ApiResponse<ExecutiveSummaryReportDto>.Ok(report));
    }

    // ── Project Reports ──────────────────────────────────────────────

    /// <summary>
    /// Get summary list of all projects with vulnerability and package counts.
    /// </summary>
    [HttpGet("projects")]
    [ProducesResponseType(typeof(ApiResponse<List<ProjectSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjectSummaries([FromQuery] Guid? scanRunId, CancellationToken ct)
    {
        var summaries = await reportService.GetProjectSummariesAsync(scanRunId, ct);
        return Ok(ApiResponse<List<ProjectSummaryDto>>.Ok(summaries));
    }

    /// <summary>
    /// Get detailed report for a specific project including all repositories,
    /// packages discovered, and vulnerabilities found.
    /// </summary>
    [HttpGet("projects/{projectId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProjectDetailReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectReport(Guid projectId, [FromQuery] Guid? scanRunId, CancellationToken ct)
    {
        var report = await reportService.GetProjectReportAsync(projectId, scanRunId, ct);
        if (report is null)
            return NotFound(ApiResponse.Fail("Project not found."));
        return Ok(ApiResponse<ProjectDetailReportDto>.Ok(report));
    }

    /// <summary>
    /// Export project report as CSV file.
    /// </summary>
    [HttpGet("projects/{projectId:guid}/export/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportProjectCsv(Guid projectId, [FromQuery] Guid? scanRunId, CancellationToken ct)
    {
        var report = await reportService.GetProjectReportAsync(projectId, scanRunId, ct);
        if (report is null)
            return NotFound(ApiResponse.Fail("Project not found."));

        var csv = new StringBuilder();
        csv.AppendLine("Project,Repository,Ecosystem,Package,Version,SourceFile,HasVulnerabilities,PURL");

        foreach (var repo in report.Repositories)
        {
            foreach (var pkg in repo.TopPackages)
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsv(report.ProjectName),
                    EscapeCsv(repo.RepositoryName),
                    pkg.Ecosystem,
                    EscapeCsv(pkg.Name),
                    pkg.Version,
                    EscapeCsv(pkg.SourceFile ?? ""),
                    pkg.HasVulnerabilities,
                    pkg.Purl ?? ""));
            }
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        var fileName = $"project-{report.ProjectName.Replace(" ", "-")}-report.csv";
        return File(bytes, "text/csv", fileName);
    }

    /// <summary>
    /// Export project vulnerabilities as CSV.
    /// </summary>
    [HttpGet("projects/{projectId:guid}/export/vulnerabilities-csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportProjectVulnerabilitiesCsv(
        Guid projectId, [FromQuery] Guid? scanRunId, CancellationToken ct)
    {
        var report = await reportService.GetProjectReportAsync(projectId, scanRunId, ct);
        if (report is null)
            return NotFound(ApiResponse.Fail("Project not found."));

        var csv = new StringBuilder();
        csv.AppendLine("Project,Repository,CVE,Package,InstalledVersion,FixedVersion,Severity,CVSS,Status,AgeDays,Description");

        foreach (var repo in report.Repositories)
        {
            foreach (var vuln in repo.Vulnerabilities)
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsv(report.ProjectName),
                    EscapeCsv(repo.RepositoryName),
                    vuln.CveId,
                    EscapeCsv(vuln.PackageName),
                    vuln.InstalledVersion,
                    vuln.FixedVersion ?? "",
                    vuln.Severity,
                    vuln.CvssScore?.ToString("F1") ?? "",
                    vuln.Status,
                    vuln.AgeDays,
                    EscapeCsv(vuln.Description ?? "")));
            }
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        var fileName = $"project-{report.ProjectName.Replace(" ", "-")}-vulnerabilities.csv";
        return File(bytes, "text/csv", fileName);
    }

    // ── Vulnerability Reports ────────────────────────────────────────

    /// <summary>
    /// Get summary list of all unique CVEs with affected repo counts,
    /// optionally filtered by severity.
    /// </summary>
    [HttpGet("vulnerabilities")]
    [ProducesResponseType(typeof(ApiResponse<List<VulnerabilitySummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVulnerabilitySummaries(
        [FromQuery] string? severity, [FromQuery] Guid? scanRunId, CancellationToken ct)
    {
        var summaries = await reportService.GetVulnerabilitySummariesAsync(severity, scanRunId, ct);
        return Ok(ApiResponse<List<VulnerabilitySummaryDto>>.Ok(summaries));
    }

    /// <summary>
    /// Get detailed report for a specific CVE showing all affected
    /// repositories and packages across the codebase.
    /// </summary>
    [HttpGet("vulnerabilities/{cveId}")]
    [ProducesResponseType(typeof(ApiResponse<VulnerabilityDetailReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVulnerabilityReport(string cveId, [FromQuery] Guid? scanRunId, CancellationToken ct)
    {
        var report = await reportService.GetVulnerabilityReportAsync(cveId, scanRunId, ct);
        if (report is null)
            return NotFound(ApiResponse.Fail($"No vulnerability found with CVE: {cveId}"));
        return Ok(ApiResponse<VulnerabilityDetailReportDto>.Ok(report));
    }

    /// <summary>
    /// Export all vulnerabilities as CSV, optionally filtered by severity.
    /// </summary>
    [HttpGet("vulnerabilities/export/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportVulnerabilitiesCsv(
        [FromQuery] string? severity, [FromQuery] Guid? scanRunId, CancellationToken ct)
    {
        var summaries = await reportService.GetVulnerabilitySummariesAsync(severity, scanRunId, ct);

        var csv = new StringBuilder();
        csv.AppendLine("CVE,Severity,CVSS,Package,FixedVersion,AffectedRepos,Occurrences,Description");

        foreach (var v in summaries)
        {
            csv.AppendLine(string.Join(",",
                v.CveId,
                v.Severity,
                v.CvssScore?.ToString("F1") ?? "",
                EscapeCsv(v.PackageName),
                v.FixedVersion ?? "",
                v.AffectedRepositories,
                v.TotalOccurrences,
                EscapeCsv(v.Description ?? "")));
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", "vulnerabilities-report.csv");
    }

    // ── Severity Trends ──────────────────────────────────────────────

    /// <summary>
    /// Get vulnerability severity trend data across recent scans for chart visualization.
    /// </summary>
    [HttpGet("trends")]
    [ProducesResponseType(typeof(ApiResponse<List<SeverityTrendDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSeverityTrends([FromQuery] int count = 10, CancellationToken ct = default)
    {
        var trends = await reportService.GetSeverityTrendsAsync(count, ct);
        return Ok(ApiResponse<List<SeverityTrendDto>>.Ok(trends));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
