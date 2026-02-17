using Vulscan.Domain.Common;

namespace Vulscan.Domain.Entities;

/// <summary>
/// Git repository discovered from Azure DevOps.
/// </summary>
public class Repository : BaseEntity
{
    public int ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CloneUrl { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = "main";
    public string? LastScannedCommit { get; set; }
    public DateTime? LastScannedAt { get; set; }
    public bool IsEnabled { get; set; } = true;

    // Navigation
    public Project Project { get; set; } = null!;
    public ICollection<Sbom> Sboms { get; set; } = [];
    public ICollection<Vulnerability> Vulnerabilities { get; set; } = [];
}
