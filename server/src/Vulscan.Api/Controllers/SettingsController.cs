using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Settings;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

/// <summary>
/// Admin-only system settings: global scan schedule (cron), enable switch, etc.
/// Per-project cron overrides live on the Project itself.
/// </summary>
[ApiController]
[Route("api/v1/settings")]
[Authorize(Roles = "Admin")]
public sealed class SettingsController(IScheduleSettingsService settings) : ControllerBase
{
    [HttpGet("schedule")]
    public async Task<ActionResult<ApiResponse<ScheduleSettingsDto>>> GetSchedule(CancellationToken ct)
        => Ok(ApiResponse<ScheduleSettingsDto>.Ok(await settings.GetAsync(ct)));

    [HttpPut("schedule")]
    public async Task<ActionResult<ApiResponse<ScheduleSettingsDto>>> UpdateSchedule(
        [FromBody] UpdateScheduleSettingsRequest request, CancellationToken ct)
    {
        try
        {
            var updated = await settings.UpdateAsync(request, ct);
            return Ok(ApiResponse<ScheduleSettingsDto>.Ok(updated, "Schedule updated."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ScheduleSettingsDto>.Fail(ex.Message));
        }
    }
}
