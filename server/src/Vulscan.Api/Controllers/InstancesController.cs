using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Instances;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

/// <summary>
/// Manages Azure DevOps instance configurations.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class InstancesController(IInstanceService instanceService) : ControllerBase
{
    /// <summary>
    /// Get all configured Azure DevOps instances.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<InstanceDto>>>> GetAll()
    {
        var instances = await instanceService.GetAllAsync();
        return Ok(ApiResponse<List<InstanceDto>>.Ok(instances));
    }

    /// <summary>
    /// Get instance summaries for dropdowns.
    /// </summary>
    [HttpGet("summaries")]
    public async Task<ActionResult<ApiResponse<List<InstanceSummaryDto>>>> GetSummaries()
    {
        var summaries = await instanceService.GetSummariesAsync();
        return Ok(ApiResponse<List<InstanceSummaryDto>>.Ok(summaries));
    }

    /// <summary>
    /// Get instance by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<InstanceDto>>> GetById(int id)
    {
        var instance = await instanceService.GetByIdAsync(id);
        if (instance is null)
            return NotFound(ApiResponse.Fail("Instance not found."));

        return Ok(ApiResponse<InstanceDto>.Ok(instance));
    }

    /// <summary>
    /// Create a new Azure DevOps instance from a project URL.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    public async Task<ActionResult<ApiResponse<InstanceDto>>> Create([FromBody] CreateInstanceRequest request)
    {
        try
        {
            var instance = await instanceService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = instance.Id },
                ApiResponse<InstanceDto>.Ok(instance, "Instance created successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Update an existing instance.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    public async Task<ActionResult<ApiResponse<InstanceDto>>> Update(int id, [FromBody] UpdateInstanceRequest request)
    {
        var instance = await instanceService.UpdateAsync(id, request);
        if (instance is null)
            return NotFound(ApiResponse.Fail("Instance not found."));

        return Ok(ApiResponse<InstanceDto>.Ok(instance, "Instance updated successfully."));
    }

    /// <summary>
    /// Delete an instance.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        var deleted = await instanceService.DeleteAsync(id);
        if (!deleted)
            return NotFound(ApiResponse.Fail("Instance not found."));

        return Ok(ApiResponse.Ok("Instance deleted successfully."));
    }

    /// <summary>
    /// Test connection to an instance.
    /// </summary>
    [HttpPost("{id:int}/test")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    public async Task<ActionResult<ApiResponse>> TestConnection(int id)
    {
        var (success, message) = await instanceService.TestConnectionAsync(id);
        return success
            ? Ok(ApiResponse.Ok(message))
            : BadRequest(ApiResponse.Fail(message));
    }
}
