using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Scans;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public sealed class ScansController(
    IScanService scanService,
    ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>
    /// Manually trigger a vulnerability scan for a specific Azure DevOps instance.
    /// </summary>
    [HttpPost("trigger")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    [ProducesResponseType(typeof(ApiResponse<TriggerScanResponse>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerScan([FromBody] TriggerScanRequest request, CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User context not available.");

        var result = await scanService.TriggerScanAsync(request, userId, ct);
        return StatusCode(StatusCodes.Status202Accepted,
            ApiResponse<TriggerScanResponse>.Ok(result, "Scan triggered successfully."));
    }

    /// <summary>
    /// Get paginated scan run history.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ScanRunDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await scanService.GetScanHistoryAsync(page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<ScanRunDto>>.Ok(result));
    }

    /// <summary>
    /// Get details of a specific scan run.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ScanRunDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await scanService.GetScanByIdAsync(id, ct);
        if (result is null) return NotFound(ApiResponse.Fail("Scan run not found."));
        return Ok(ApiResponse<ScanRunDto>.Ok(result));
    }
}
