using System.ComponentModel.DataAnnotations;

namespace Vulscan.Application.DTOs.Auth;

public sealed record LoginRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Username { get; init; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 6)]
    public string Password { get; init; } = string.Empty;
}

public sealed record LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// True when the user must set a new password before doing anything else. The access token is restricted
    /// server-side until they do.
    /// </summary>
    public bool MustChangePassword { get; init; }

    public UserInfo User { get; init; } = null!;
}

public sealed record UserInfo
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string AuthProvider { get; init; } = "Local";
}

public sealed record RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed record RegisterRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Username { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;

    public string Role { get; init; } = "Viewer";
}

public sealed record EntraLoginRequest
{
    [Required]
    public string IdToken { get; init; } = string.Empty;
}

public sealed record ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record LogoutRequest
{
    public string? RefreshToken { get; init; }
}

/// <summary>Public sign-in configuration. Contains no secrets: tenant and client ids are public identifiers.</summary>
public sealed record AuthConfigDto
{
    public bool LocalEnabled { get; init; }
    public bool EntraEnabled { get; init; }
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? Authority { get; init; }
}
