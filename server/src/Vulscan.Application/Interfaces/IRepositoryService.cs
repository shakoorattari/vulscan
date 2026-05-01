using Vulscan.Application.DTOs.Projects;

namespace Vulscan.Application.Interfaces;

/// <summary>
/// Service for managing repository configurations including branch settings.
/// </summary>
public interface IRepositoryService
{
    /// <summary>Get all repositories with their configured branches for a project.</summary>
    Task<List<RepositoryConfigDto>> GetRepositoriesByProjectAsync(Guid projectId, CancellationToken ct = default);
    
    /// <summary>Get a single repository with its configured branches.</summary>
    Task<RepositoryConfigDto?> GetRepositoryByIdAsync(Guid repositoryId, CancellationToken ct = default);
    
    /// <summary>Update repository settings (enabled status, default branch).</summary>
    Task<RepositoryConfigDto?> UpdateRepositoryAsync(Guid repositoryId, UpdateRepositoryRequest request, CancellationToken ct = default);
    
    /// <summary>Add a new branch to a repository for scanning.</summary>
    Task<BranchConfigDto> AddBranchAsync(Guid repositoryId, AddBranchRequest request, CancellationToken ct = default);
    
    /// <summary>Update branch configuration (enabled status).</summary>
    Task<BranchConfigDto?> UpdateBranchAsync(Guid repositoryId, Guid branchId, UpdateBranchRequest request, CancellationToken ct = default);
    
    /// <summary>Remove a configured branch from a repository.</summary>
    Task<bool> DeleteBranchAsync(Guid repositoryId, Guid branchId, CancellationToken ct = default);
    
    /// <summary>Get all configured branches for a repository.</summary>
    Task<List<BranchConfigDto>> GetBranchesByRepositoryAsync(Guid repositoryId, CancellationToken ct = default);
}
