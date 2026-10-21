using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulscan.Application.DTOs.Audit;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

/// <summary>Read-only access to the audit trail. Restricted to administrators.</summary>
[ApiController]
[Route("api/v1/audit-logs")]
[Authorize(Roles = "Admin")]
public sealed class AuditLogsController(IAuditLogQueryService auditLogs) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuditLogDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] AuditLogFilterDto filter, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<AuditLogDto>>.Ok(await auditLogs.GetPageAsync(filter, ct)));

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] AuditLogFilterDto filter, CancellationToken ct)
    {
        var csv = await auditLogs.ExportCsvAsync(filter, ct: ct);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"audit-log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }
}
