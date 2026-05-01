using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Projects;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

/// <summary>
/// Manage repository configurations including branch settings.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public sealed class RepositoriesController(IRepositoryService repositoryService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RepositoryConfigDto>>> GetById(Guid id, CancellationToken ct)
    {
        var repo = await repositoryService.GetRepositoryByIdAsync(id, ct);
        return repo is null
            ? NotFound(ApiResponse.Fail("Repository not found."))
            : Ok(ApiResponse<RepositoryConfigDto>.Ok(repo));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    public async Task<ActionResult<ApiResponse<RepositoryConfigDto>>> Update(
        Guid id, 
        [FromBody] UpdateRepositoryRequest request, 
        CancellationToken ct)
    {
        var repo = await repositoryService.UpdateRepositoryAsync(id, request, ct);
        return repo is null
            ? NotFound(ApiResponse.Fail("Repository not found."))
            : Ok(ApiResponse<RepositoryConfigDto>.Ok(repo, "Repository updated."));
    }

    [HttpGet("{id:guid}/branches")]
    public async Task<ActionResult<ApiResponse<List<BranchConfigDto>>>> GetBranches(Guid id, CancellationToken ct)
    {
        var branches = await repositoryService.GetBranchesByRepositoryAsync(id, ct);
        return Ok(ApiResponse<List<BranchConfigDto>>.Ok(branches));
    }

    [HttpPost("{id:guid}/branches")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    public async Task<ActionResult<ApiResponse<BranchConfigDto>>> AddBranch(
        Guid id, 
        [FromBody] AddBranchRequest request, 
        CancellationToken ct)
    {
        try
        {
            var branch = await repositoryService.AddBranchAsync(id, request, ct);
            return Ok(ApiResponse<BranchConfigDto>.Ok(branch, "Branch added."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("{repoId:guid}/branches/{branchId:guid}")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    public async Task<ActionResult<ApiResponse<BranchConfigDto>>> UpdateBranch(
        Guid repoId, 
        Guid branchId, 
        [FromBody] UpdateBranchRequest request, 
        CancellationToken ct)
    {
        var branch = await repositoryService.UpdateBranchAsync(repoId, branchId, request, ct);
        return branch is null
            ? NotFound(ApiResponse.Fail("Branch not found."))
            : Ok(ApiResponse<BranchConfigDto>.Ok(branch, "Branch updated."));
    }

    [HttpDelete("{repoId:guid}/branches/{branchId:guid}")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    public async Task<ActionResult<ApiResponse>> DeleteBranch(Guid repoId, Guid branchId, CancellationToken ct)
    {
        return await repositoryService.DeleteBranchAsync(repoId, branchId, ct)
            ? Ok(ApiResponse.Ok("Branch removed."))
            : NotFound(ApiResponse.Fail("Branch not found."));
    }
}
