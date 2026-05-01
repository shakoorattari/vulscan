using System.ComponentModel.DataAnnotations;

namespace Vulscan.Application.DTOs.Projects;

/// <summary>
/// DTO for a repository with its configured branches for scanning.
/// </summary>
public record RepositoryConfigDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public required string CloneUrl { get; init; }
    public required string DefaultBranch { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime? LastScannedAt { get; init; }
    public string? LastScannedCommit { get; init; }
    public List<BranchConfigDto> ConfiguredBranches { get; init; } = [];
    
    /// <summary>Total number of branches configured for scanning.</summary>
    public int TotalBranches => ConfiguredBranches.Count;
    
    /// <summary>Number of enabled branches.</summary>
    public int EnabledBranches => ConfiguredBranches.Count(b => b.IsEnabled);
}

/// <summary>
/// DTO for a configured branch within a repository.
/// </summary>
public record BranchConfigDto
{
    public Guid Id { get; init; }
    public Guid RepositoryId { get; init; }
    public required string BranchName { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime? LastScannedAt { get; init; }
    public string? LastScannedCommit { get; init; }
    public int ScanCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Request to add a new branch to a repository for scanning.
/// </summary>
public record AddBranchRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string BranchName { get; init; } = string.Empty;
    
    /// <summary>Whether the branch is enabled for scanning by default.</summary>
    public bool IsEnabled { get; init; } = true;
}

/// <summary>
/// Request to update a branch configuration.
/// </summary>
public record UpdateBranchRequest
{
    public bool IsEnabled { get; init; }
}

/// <summary>
/// Request to update repository settings.
/// </summary>
public record UpdateRepositoryRequest
{
    public bool IsEnabled { get; init; }
    public string? DefaultBranch { get; init; }
}

/// <summary>
/// Project configuration details including all repositories and their branches.
/// </summary>
public record ProjectConfigurationDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public bool IsEnabled { get; init; }
    public string? DefaultBranch { get; init; }
    public List<RepositoryConfigDto> Repositories { get; init; } = [];
    
    /// <summary>Total number of repositories in this project.</summary>
    public int TotalRepositories => Repositories.Count;
    
    /// <summary>Total number of configured branches across all repositories.</summary>
    public int TotalConfiguredBranches => Repositories.Sum(r => r.TotalBranches);
    
    /// <summary>Number of enabled repositories.</summary>
    public int EnabledRepositories => Repositories.Count(r => r.IsEnabled);
}
