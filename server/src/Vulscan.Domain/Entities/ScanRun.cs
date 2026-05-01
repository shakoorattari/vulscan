using Vulscan.Domain.Common;
using Vulscan.Domain.Enums;

namespace Vulscan.Domain.Entities;

/// <summary>
/// Represents a single scan execution run for a specific Project.
/// Scans are always scoped to a single project.
/// </summary>
public class ScanRun : BaseEntity
{
    /// <summary>Required — every scan targets exactly one project.</summary>
    public Guid ProjectId { get; set; }

    public Guid? TriggeredByUserId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int DurationSeconds { get; set; }
    public ScanStatus Status { get; set; } = ScanStatus.Queued;
    public int ReposScanned { get; set; }
    public int ReposFailed { get; set; }
    public int TotalVulnerabilities { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
    public string? ErrorLog { get; set; }

    // Navigation
    public Project Project { get; set; } = null!;
    public User? TriggeredBy { get; set; }
    public ICollection<Sbom> Sboms { get; set; } = [];
    public ICollection<Vulnerability> Vulnerabilities { get; set; } = [];
}
