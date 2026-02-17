using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Vulnerabilities;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public sealed class VulnerabilitiesController(IVulnerabilityService vulnerabilityService) : ControllerBase
{
    /// <summary>
    /// Get paginated, filterable list of vulnerabilities.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<VulnerabilityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] VulnerabilityFilterDto filter, CancellationToken ct)
    {
        var result = await vulnerabilityService.GetVulnerabilitiesAsync(filter, ct);
        return Ok(ApiResponse<PagedResult<VulnerabilityDto>>.Ok(result));
    }

    /// <summary>
    /// Get a single vulnerability by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<VulnerabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await vulnerabilityService.GetByIdAsync(id, ct);
        if (result is null) return NotFound(ApiResponse.Fail("Vulnerability not found."));
        return Ok(ApiResponse<VulnerabilityDto>.Ok(result));
    }

    /// <summary>
    /// Update the status of a vulnerability (acknowledge, resolve, suppress).
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateVulnerabilityStatusDto dto,
        CancellationToken ct)
    {
        var updated = await vulnerabilityService.UpdateStatusAsync(id, dto.Status, ct);
        if (!updated) return NotFound(ApiResponse.Fail("Vulnerability not found."));
        return Ok(ApiResponse.Ok("Vulnerability status updated."));
    }
}
