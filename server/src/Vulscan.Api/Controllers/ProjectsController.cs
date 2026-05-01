using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Projects;
using Vulscan.Application.DTOs.Scans;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

/// <summary>
/// First-class management of Azure DevOps projects (per-project scan targets).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public sealed class ProjectsController(
    IProjectService projectService,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ProjectDto>>>> GetAll(CancellationToken ct)
        => Ok(ApiResponse<List<ProjectDto>>.Ok(await projectService.GetAllAsync(ct)));

    [HttpGet("summaries")]
    public async Task<ActionResult<ApiResponse<List<ProjectSummaryDto>>>> GetSummaries(CancellationToken ct)
        => Ok(ApiResponse<List<ProjectSummaryDto>>.Ok(await projectService.GetSummariesAsync(ct)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> GetById(Guid id, CancellationToken ct)
    {
        var p = await projectService.GetByIdAsync(id, ct);
        return p is null
            ? NotFound(ApiResponse.Fail("Project not found."))
            : Ok(ApiResponse<ProjectDto>.Ok(p));
    }

    [HttpGet("{id:guid}/configuration")]
    public async Task<ActionResult<ApiResponse<ProjectConfigurationDto>>> GetConfiguration(Guid id, CancellationToken ct)
    {
        var config = await projectService.GetConfigurationAsync(id, ct);
        return config is null
            ? NotFound(ApiResponse.Fail("Project not found."))
            : Ok(ApiResponse<ProjectConfigurationDto>.Ok(config));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> Create(
        [FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        try
        {
            var p = await projectService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = p.Id },
                ApiResponse<ProjectDto>.Ok(p, "Project created."));
        }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> Update(
        Guid id, [FromBody] UpdateProjectRequest request, CancellationToken ct)
    {
        var p = await projectService.UpdateAsync(id, request, ct);
        return p is null
            ? NotFound(ApiResponse.Fail("Project not found."))
            : Ok(ApiResponse<ProjectDto>.Ok(p, "Project updated."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        return await projectService.DeleteAsync(id, ct)
            ? Ok(ApiResponse.Ok("Project deleted."))
            : NotFound(ApiResponse.Fail("Project not found."));
    }

    [HttpPost("{id:guid}/enable")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> Enable(Guid id, CancellationToken ct)
    {
        var p = await projectService.SetEnabledAsync(id, true, ct);
        return p is null ? NotFound(ApiResponse.Fail("Project not found.")) : Ok(ApiResponse<ProjectDto>.Ok(p));
    }

    [HttpPost("{id:guid}/disable")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> Disable(Guid id, CancellationToken ct)
    {
        var p = await projectService.SetEnabledAsync(id, false, ct);
        return p is null ? NotFound(ApiResponse.Fail("Project not found.")) : Ok(ApiResponse<ProjectDto>.Ok(p));
    }

    [HttpPost("{id:guid}/scan")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    [ProducesResponseType(typeof(ApiResponse<TriggerScanResponse>), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> TriggerScan(Guid id, CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User context not available.");
        try
        {
            var result = await projectService.TriggerScanAsync(id, userId, ct);
            return StatusCode(StatusCodes.Status202Accepted,
                ApiResponse<TriggerScanResponse>.Ok(result, "Scan queued."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }
}
