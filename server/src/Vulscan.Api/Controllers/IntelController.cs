using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Intel;
using Vulscan.Application.Intel;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

/// <summary>
/// Everything known about an advisory — CVSS, dates, weaknesses, fixes, references, CISA KEV and EPSS — gathered
/// from OSV, NVD, FIRST and CISA and cached. See docs/VULNERABILITY_INTELLIGENCE.md.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class IntelController(IVulnerabilityIntelService intel) : ControllerBase
{
    /// <summary>The stored record, fetched from the sources first if it is missing or past its refresh time.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<VulnerabilityIntelDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        if (!IntelId.IsValid(id))
            return BadRequest(ApiResponse.Fail("Not a valid advisory id (expected something like CVE-2021-44228 or GHSA-xxxx-xxxx-xxxx)."));

        var record = await intel.GetAsync(id, refresh: false, ct);
        return record is null
            ? NotFound(ApiResponse.Fail($"No source knows {id}."))
            : Ok(ApiResponse<VulnerabilityIntelDto>.Ok(record));
    }

    /// <summary>Asks the sources again now, ignoring the stored record's freshness.</summary>
    [HttpPost("{id}/refresh")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    [ProducesResponseType(typeof(ApiResponse<VulnerabilityIntelDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Refresh(string id, CancellationToken ct)
    {
        if (!IntelId.IsValid(id))
            return BadRequest(ApiResponse.Fail("Not a valid advisory id."));

        var record = await intel.GetAsync(id, refresh: true, ct);
        return record is null
            ? NotFound(ApiResponse.Fail($"No source knows {id}."))
            : Ok(ApiResponse<VulnerabilityIntelDto>.Ok(record));
    }
}
