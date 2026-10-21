using System.ComponentModel.DataAnnotations;

namespace Vulscan.Application.DTOs.Users;

public sealed record UserDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string AuthProvider { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }

    /// <summary>Set only while the account is currently locked out.</summary>
    public DateTime? LockedUntil { get; init; }
    public bool MustChangePassword { get; init; }

    public DateTime? LastAccessReviewAt { get; init; }
    public string? LastAccessReviewBy { get; init; }

    /// <summary>True when the account has never been reviewed, or the last review is older than the configured interval.</summary>
    public bool ReviewOverdue { get; init; }

    public DateTime? DeactivatedAt { get; init; }
    public string? DeactivationReason { get; init; }
}

public sealed record UpdateUserRoleRequest
{
    [Required]
    public string Role { get; init; } = string.Empty;
}

public sealed record DeactivateUserRequest
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record ResetPasswordRequest
{
    [Required]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record ReviewUserRequest
{
    [StringLength(500)]
    public string? Note { get; init; }
}
