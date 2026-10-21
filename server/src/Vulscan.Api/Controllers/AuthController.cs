using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Vulscan.Application.DTOs.Auth;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>Which sign-in methods are available (and, for Entra, the public ids the SPA needs). No secrets.</summary>
    [HttpGet("config")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthConfigDto>), StatusCodes.Status200OK)]
    public IActionResult GetConfig() => Ok(ApiResponse<AuthConfigDto>.Ok(authService.GetConfig()));

    /// <summary>Authenticate with username and password.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ClientIp, ct);
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Login successful."));
    }

    /// <summary>Exchange a Microsoft Entra ID token for a Vulscan session.</summary>
    [HttpPost("entra")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginWithEntra([FromBody] EntraLoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginWithEntraAsync(request.IdToken, ClientIp, ct);
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Login successful."));
    }

    /// <summary>Rotate a refresh token for a new access token and refresh token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await authService.RefreshTokenAsync(request.RefreshToken, ClientIp, ct);
        return Ok(ApiResponse<LoginResponse>.Ok(result));
    }

    /// <summary>End the session (revokes its refresh tokens). Works with an expired access token.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        await authService.LogoutAsync(request.RefreshToken, ct);
        return Ok(ApiResponse.Ok("Signed out."));
    }

    /// <summary>Change the signed-in user's password. Ends all other sessions and returns a fresh one.</summary>
    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId)) return Unauthorized(ApiResponse.Fail("Invalid session."));

        var result = await authService.ChangePasswordAsync(userId, request, ClientIp, ct);
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Password changed."));
    }

    /// <summary>Register a new local user (admin only). The user must change the password at first sign-in.</summary>
    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<UserInfo>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await authService.RegisterAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<UserInfo>.Ok(result, "User registered. They must change the password at first sign-in."));
    }
}
