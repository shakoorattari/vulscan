using System.ComponentModel.DataAnnotations;

namespace Vulscan.Application.DTOs.Projects;

/// <summary>
/// Create a project with its own credentials (per-project mode).
/// The Azure DevOps server (instance) is auto-found-or-created from the URL.
/// </summary>
public record CreateProjectRequest
{
    [Required] public string Name { get; init; } = string.Empty;
    /// <summary>Full URL, e.g. https://devops.ishj.ae/SDD/CTS</summary>
    [Required] public string ProjectUrl { get; init; } = string.Empty;
    [Required] public string Username { get; init; } = string.Empty;
    [Required] public string Password { get; init; } = string.Empty;
    /// <summary>Override branch (optional - falls back to repo default branch).</summary>
    public string? DefaultBranch { get; init; }

    /// <summary>Optional 5-field cron (UTC). When null, the global schedule applies.</summary>
    public string? CronExpression { get; init; }
}

public record UpdateProjectRequest
{
    [Required] public string Name { get; init; } = string.Empty;
    /// <summary>Optional — only update credentials when both username and password are provided.</summary>
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? DefaultBranch { get; init; }
    /// <summary>Optional 5-field cron (UTC). Empty/null clears the override (use global).</summary>
    public string? CronExpression { get; init; }
    public bool IsEnabled { get; init; } = true;
    
    // Owner information for email notifications
    public string? OwnerName { get; init; }
    public string? OwnerEmail { get; init; }
    public string? CcEmails { get; init; }
    public bool SendEmailNotifications { get; init; } = true;
}

public record ProjectDto
{
    public Guid Id { get; init; }
    public Guid InstanceId { get; init; }
    public required string InstanceName { get; init; }
    public required string Name { get; init; }
    public required string AzureProjectId { get; init; }
    public required string Url { get; init; }
    public string? DefaultBranch { get; init; }
    public bool IsEnabled { get; init; }
    public bool HasOwnCredentials { get; init; }
    /// <summary>Per-project cron override (null when project uses the global schedule).</summary>
    public string? CronExpression { get; init; }
    /// <summary>Effective cron actually used for this project (override or global).</summary>
    public string? EffectiveCron { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastScannedAt { get; init; }

    public int RepositoryCount { get; init; }

    /// <summary>Number of scans ever run (history).</summary>
    public int TotalScans { get; init; }

    /// <summary>Vulnerabilities in the latest completed scan — not a sum over every scan.</summary>
    public int TotalVulnerabilities { get; init; }

    // Owner information
    public string? OwnerName { get; init; }
    public string? OwnerEmail { get; init; }
    public string? CcEmails { get; init; }
    public bool SendEmailNotifications { get; init; }

    // Scan snapshot. The status is that of the newest scan (so Running/Failed are visible); the id, duration and
    // counts are those of the latest *completed* scan, so a scan in progress or a failed one never shows as "0".
    public Guid? LastScanId { get; init; }
    public string? LastScanStatus { get; init; }
    public int? LastScanDurationSeconds { get; init; }

    /// <summary>When the scan behind the counts finished; null means the project has no completed scan yet.</summary>
    public DateTime? ResultsAsOf { get; init; }

    /// <summary>A scan is queued or running right now.</summary>
    public bool ScanInProgress { get; init; }
    public int LastScanCriticalCount { get; init; }
    public int LastScanHighCount { get; init; }
    public int LastScanMediumCount { get; init; }
    public int LastScanLowCount { get; init; }
    public int LastScanTotalVulnerabilities { get; init; }
}

public record ProjectSummaryDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public bool IsEnabled { get; init; }
}
