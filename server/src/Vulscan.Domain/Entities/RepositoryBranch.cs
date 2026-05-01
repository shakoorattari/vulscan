using Vulscan.Domain.Common;

namespace Vulscan.Domain.Entities;

/// <summary>
/// Represents a configured branch for scanning within a repository.
/// Each repository can have multiple branches configured for scanning.
/// </summary>
public class RepositoryBranch : BaseEntity
{
    public Guid RepositoryId { get; set; }
    
    /// <summary>Name of the branch (e.g., "main", "develop", "release/v1.0").</summary>
    public string BranchName { get; set; } = string.Empty;
    
    /// <summary>Whether this branch is enabled for scanning.</summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>Last commit SHA scanned for this branch.</summary>
    public string? LastScannedCommit { get; set; }
    
    /// <summary>When this branch was last scanned.</summary>
    public DateTime? LastScannedAt { get; set; }
    
    /// <summary>Number of scans performed on this branch.</summary>
    public int ScanCount { get; set; }
    
    // Navigation
    public Repository Repository { get; set; } = null!;
}
