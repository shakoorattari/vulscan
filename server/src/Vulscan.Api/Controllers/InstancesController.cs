using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Instances;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

/// <summary>
/// Read/manage Azure DevOps server (URL + collection) configurations.
/// Use <c>POST /projects</c> or <c>POST /discovery/list</c> to create instances implicitly.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class InstancesController(IInstanceService instanceService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<InstanceDto>>>> GetAll()
        => Ok(ApiResponse<List<InstanceDto>>.Ok(await instanceService.GetAllAsync()));

    [HttpGet("summaries")]
    public async Task<ActionResult<ApiResponse<List<InstanceSummaryDto>>>> GetSummaries()
        => Ok(ApiResponse<List<InstanceSummaryDto>>.Ok(await instanceService.GetSummariesAsync()));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<InstanceDto>>> GetById(Guid id)
    {
        var instance = await instanceService.GetByIdAsync(id);
        return instance is null
            ? NotFound(ApiResponse.Fail("Instance not found."))
            : Ok(ApiResponse<InstanceDto>.Ok(instance));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    public async Task<ActionResult<ApiResponse<InstanceDto>>> Update(Guid id, [FromBody] UpdateInstanceRequest request)
    {
        var instance = await instanceService.UpdateAsync(id, request);
        return instance is null
            ? NotFound(ApiResponse.Fail("Instance not found."))
            : Ok(ApiResponse<InstanceDto>.Ok(instance, "Instance updated."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        return await instanceService.DeleteAsync(id)
            ? Ok(ApiResponse.Ok("Instance deleted."))
            : NotFound(ApiResponse.Fail("Instance not found."));
    }

    [HttpPost("{id:guid}/test")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    public async Task<ActionResult<ApiResponse>> TestConnection(Guid id)
    {
        var (success, message) = await instanceService.TestConnectionAsync(id);
        return success ? Ok(ApiResponse.Ok(message)) : BadRequest(ApiResponse.Fail(message));
    }
}
