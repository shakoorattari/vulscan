using Vulscan.Domain.Common;

namespace Vulscan.Domain.Entities;

/// <summary>
/// Software Bill of Materials generated for a repository during a scan.
/// </summary>
public class Sbom : BaseEntity
{
    public Guid RepositoryId { get; set; }
    public Guid ScanRunId { get; set; }
    
    /// <summary>Branch that was scanned to generate this SBOM.</summary>
    public string BranchName { get; set; } = string.Empty;
    
    public string Format { get; set; } = "CycloneDX";
    public string Generator { get; set; } = "Syft";
    public int ComponentCount { get; set; }
    public string? SbomJson { get; set; }
    public string? CommitHash { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int GenerationDurationMs { get; set; }

    // Navigation
    public Repository Repository { get; set; } = null!;
    public ScanRun ScanRun { get; set; } = null!;
    public ICollection<Vulnerability> Vulnerabilities { get; set; } = [];
}
