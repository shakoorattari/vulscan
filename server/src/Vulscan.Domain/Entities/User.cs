using Vulscan.Domain.Common;
using Vulscan.Domain.Enums;

namespace Vulscan.Domain.Entities;

/// <summary>
/// A person who can sign in to Vulscan (the SDD security team). Access is never granted implicitly:
/// every user has an explicit role, and can be deactivated without being deleted so history stays attributable.
/// </summary>
public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>BCrypt hash. Null for Entra users, who have no Vulscan password.</summary>
    public string? PasswordHash { get; set; }

    public UserRole Role { get; set; } = UserRole.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    // ── Identity source ─────────────────────────────────────────────────
    public AuthProvider AuthProvider { get; set; } = AuthProvider.Local;

    /// <summary>Stable external identifier (Entra object id, "oid"). Null for local users.</summary>
    public string? ExternalId { get; set; }

    // ── Secure log-on ───────────────────────────────────────────────────
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndAt { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime? PasswordChangedAt { get; set; }

    // ── Access lifecycle (registration, review, removal) ────────────────
    /// <summary>
    /// When access was last granted: creation, first Entra provisioning, or reactivation. With <see cref="LastLoginAt"/>
    /// this drives the dormancy clock, so a reactivated account is not immediately deactivated again.
    /// Null on rows that pre-date the column (treated as <see cref="BaseEntity.CreatedAt"/>).
    /// </summary>
    public DateTime? AccessGrantedAt { get; set; }

    public DateTime? LastAccessReviewAt { get; set; }
    public string? LastAccessReviewBy { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public string? DeactivationReason { get; set; }

    // Navigation
    public ICollection<ScanRun> TriggeredScans { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
