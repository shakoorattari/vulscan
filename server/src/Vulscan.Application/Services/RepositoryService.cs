using Microsoft.EntityFrameworkCore;
using Vulscan.Application.DTOs.Projects;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;

namespace Vulscan.Application.Services;

public sealed class RepositoryService(DbContext dbContext) : IRepositoryService
{
    public async Task<List<RepositoryConfigDto>> GetRepositoriesByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var repositories = await dbContext.Set<Repository>()
            .Include(r => r.ConfiguredBranches)
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        return repositories.Select(MapToDto).ToList();
    }

    public async Task<RepositoryConfigDto?> GetRepositoryByIdAsync(Guid repositoryId, CancellationToken ct = default)
    {
        var repository = await dbContext.Set<Repository>()
            .Include(r => r.ConfiguredBranches)
            .FirstOrDefaultAsync(r => r.Id == repositoryId, ct);

        return repository is null ? null : MapToDto(repository);
    }

    public async Task<RepositoryConfigDto?> UpdateRepositoryAsync(
        Guid repositoryId, 
        UpdateRepositoryRequest request, 
        CancellationToken ct = default)
    {
        var repository = await dbContext.Set<Repository>()
            .Include(r => r.ConfiguredBranches)
            .FirstOrDefaultAsync(r => r.Id == repositoryId, ct);

        if (repository is null) return null;

        repository.IsEnabled = request.IsEnabled;
        if (!string.IsNullOrWhiteSpace(request.DefaultBranch))
        {
            repository.DefaultBranch = request.DefaultBranch.Trim();
        }
        repository.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);
        return MapToDto(repository);
    }

    public async Task<BranchConfigDto> AddBranchAsync(
        Guid repositoryId, 
        AddBranchRequest request, 
        CancellationToken ct = default)
    {
        var repository = await dbContext.Set<Repository>()
            .Include(r => r.ConfiguredBranches)
            .FirstOrDefaultAsync(r => r.Id == repositoryId, ct)
            ?? throw new InvalidOperationException($"Repository {repositoryId} not found.");

        // Check if branch already exists
        var branchName = request.BranchName.Trim();
        if (repository.ConfiguredBranches.Any(b => b.BranchName.Equals(branchName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Branch '{branchName}' is already configured for this repository.");
        }

        var branch = new RepositoryBranch
        {
            RepositoryId = repositoryId,
            BranchName = branchName,
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.Set<RepositoryBranch>().Add(branch);
        await dbContext.SaveChangesAsync(ct);

        return MapToBranchDto(branch);
    }

    public async Task<BranchConfigDto?> UpdateBranchAsync(
        Guid repositoryId, 
        Guid branchId, 
        UpdateBranchRequest request, 
        CancellationToken ct = default)
    {
        var branch = await dbContext.Set<RepositoryBranch>()
            .FirstOrDefaultAsync(b => b.Id == branchId && b.RepositoryId == repositoryId, ct);

        if (branch is null) return null;

        branch.IsEnabled = request.IsEnabled;
        branch.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);
        return MapToBranchDto(branch);
    }

    public async Task<bool> DeleteBranchAsync(Guid repositoryId, Guid branchId, CancellationToken ct = default)
    {
        var branch = await dbContext.Set<RepositoryBranch>()
            .FirstOrDefaultAsync(b => b.Id == branchId && b.RepositoryId == repositoryId, ct);

        if (branch is null) return false;

        dbContext.Set<RepositoryBranch>().Remove(branch);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<BranchConfigDto>> GetBranchesByRepositoryAsync(Guid repositoryId, CancellationToken ct = default)
    {
        var branches = await dbContext.Set<RepositoryBranch>()
            .Where(b => b.RepositoryId == repositoryId)
            .OrderBy(b => b.BranchName)
            .ToListAsync(ct);

        return branches.Select(MapToBranchDto).ToList();
    }

    private static RepositoryConfigDto MapToDto(Repository r)
    {
        return new RepositoryConfigDto
        {
            Id = r.Id,
            ProjectId = r.ProjectId,
            Name = r.Name,
            CloneUrl = r.CloneUrl,
            DefaultBranch = r.DefaultBranch,
            IsEnabled = r.IsEnabled,
            LastScannedAt = r.LastScannedAt,
            LastScannedCommit = r.LastScannedCommit,
            ConfiguredBranches = r.ConfiguredBranches
                .Select(MapToBranchDto)
                .OrderBy(b => b.BranchName)
                .ToList(),
        };
    }

    private static BranchConfigDto MapToBranchDto(RepositoryBranch b)
    {
        return new BranchConfigDto
        {
            Id = b.Id,
            RepositoryId = b.RepositoryId,
            BranchName = b.BranchName,
            IsEnabled = b.IsEnabled,
            LastScannedAt = b.LastScannedAt,
            LastScannedCommit = b.LastScannedCommit,
            ScanCount = b.ScanCount,
            CreatedAt = b.CreatedAt,
        };
    }
}
