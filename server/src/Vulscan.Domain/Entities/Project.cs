using Vulscan.Domain.Common;

namespace Vulscan.Domain.Entities;

/// <summary>
/// Azure DevOps project — first-class scannable entity.
/// Each project has its own credentials, default branch, and scan history.
/// Belongs to an <see cref="AzureDevOpsInstance"/> (server URL + collection).
/// </summary>
public class Project : BaseEntity
{
    public Guid InstanceId { get; set; }

    /// <summary>Friendly display name (user-supplied).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Azure DevOps project name as it appears in the URL (e.g. "CTS").</summary>
    public string AzureProjectId { get; set; } = string.Empty;

    /// <summary>Full URL to the Azure DevOps project (e.g. https://devops.ishj.ae/SDD/CTS).</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Per-project credentials JSON (username/password/PAT). When null, falls back to
    /// the parent <see cref="AzureDevOpsInstance.CredentialReference"/> (used by discovery flow).
    /// </summary>
    public string? CredentialReference { get; set; }

    /// <summary>Branch to scan when the repository default branch is not desired (optional).</summary>
    public string? DefaultBranch { get; set; }

    /// <summary>Whether this project is enabled for scheduled scans / triggers.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Optional per-project cron expression (5-field, UTC). When set, overrides the
    /// global <see cref="ScheduleSettings.CronExpression"/>. When null, the global cron applies.
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>Project owner name for email notifications.</summary>
    public string? OwnerName { get; set; }

    /// <summary>Project owner email address for scan result notifications.</summary>
    public string? OwnerEmail { get; set; }

    /// <summary>
    /// Additional email addresses (comma-separated) to CC on scan notifications.
    /// </summary>
    public string? CcEmails { get; set; }

    /// <summary>Whether to send email notifications after scan completion.</summary>
    public bool SendEmailNotifications { get; set; } = true;

    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastScannedAt { get; set; }

    // Navigation
    public AzureDevOpsInstance Instance { get; set; } = null!;
    public ICollection<Repository> Repositories { get; set; } = [];
    public ICollection<ScanRun> ScanRuns { get; set; } = [];
}
