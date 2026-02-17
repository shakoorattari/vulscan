using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Dashboard;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>
    /// Get executive dashboard summary (severity counts, recent scans, top vulnerable repos).
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<DashboardSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var summary = await dashboardService.GetSummaryAsync(ct);
        return Ok(ApiResponse<DashboardSummaryDto>.Ok(summary));
    }
}
