using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulscan.Application.DTOs.Auth;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Users;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

/// <summary>Access lifecycle for the SDD security team's accounts: registration, privileges, review, removal.</summary>
[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin")]
public sealed class UsersController(IUserService users, IAuthService auth) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(ApiResponse<List<UserDto>>.Ok(await users.GetAllAsync(ct)));

    /// <summary>Create a local user. Entra users are provisioned automatically at their first Microsoft sign-in.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var created = await auth.RegisterAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<UserInfo>.Ok(created, "User created. They must change the password at first sign-in."));
    }

    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> SetRole(Guid id, [FromBody] UpdateUserRoleRequest request, CancellationToken ct)
        => Ok(ApiResponse<UserDto>.Ok(await users.SetRoleAsync(id, request.Role, ct), "Role updated."));

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, [FromBody] DeactivateUserRequest request, CancellationToken ct)
        => Ok(ApiResponse<UserDto>.Ok(await users.DeactivateAsync(id, request.Reason, ct), "User deactivated."));

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
        => Ok(ApiResponse<UserDto>.Ok(await users.ActivateAsync(id, ct), "User activated."));

    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct)
        => Ok(ApiResponse<UserDto>.Ok(await users.UnlockAsync(id, ct), "User unlocked."));

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
        => Ok(ApiResponse<UserDto>.Ok(await users.ResetPasswordAsync(id, request.NewPassword, ct),
            "Password reset. The user must change it at next sign-in."));

    /// <summary>Record that this account's access was reviewed (and found appropriate) by the signed-in administrator.</summary>
    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewUserRequest request, CancellationToken ct)
        => Ok(ApiResponse<UserDto>.Ok(await users.ReviewAsync(id, request.Note, ct), "Access review recorded."));

    [HttpGet("access-review/export/csv")]
    public async Task<IActionResult> ExportAccessReview(CancellationToken ct)
    {
        var csv = await users.ExportAccessReviewCsvAsync(ct);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"access-review-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
