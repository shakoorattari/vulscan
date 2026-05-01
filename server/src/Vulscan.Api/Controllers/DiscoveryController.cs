using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Discovery;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

/// <summary>
/// Discover Azure DevOps projects on a server using shared credentials,
/// then bulk-import selected ones as scannable Projects.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Admin,SecurityAnalyst")]
public sealed class DiscoveryController(IDiscoveryService discoveryService) : ControllerBase
{
    /// <summary>List all Azure DevOps projects available on a server using the supplied credentials.</summary>
    [HttpPost("list")]
    public async Task<ActionResult<ApiResponse<DiscoveryListResponse>>> List(
        [FromBody] DiscoveryListRequest request, CancellationToken ct)
    {
        try
        {
            var result = await discoveryService.ListProjectsAsync(request, ct);
            return Ok(ApiResponse<DiscoveryListResponse>.Ok(result));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }

    /// <summary>Bulk-import selected projects on the supplied instance.</summary>
    [HttpPost("import")]
    public async Task<ActionResult<ApiResponse<DiscoveryImportResponse>>> Import(
        [FromBody] DiscoveryImportRequest request, CancellationToken ct)
    {
        try
        {
            var result = await discoveryService.ImportProjectsAsync(request, ct);
            return Ok(ApiResponse<DiscoveryImportResponse>.Ok(result, $"Imported {result.Imported}, skipped {result.Skipped}."));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }
}
