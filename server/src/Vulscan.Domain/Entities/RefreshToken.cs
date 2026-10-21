using Vulscan.Domain.Common;

namespace Vulscan.Domain.Entities;

/// <summary>
/// A server-side record of an issued refresh token. Only a hash of the token is stored, so a database leak does
/// not yield usable sessions. Tokens rotate on every use; presenting an already-rotated token revokes the whole
/// <see cref="FamilyId"/> (the sign-in session), which is how theft of a refresh token is detected.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>SHA-256 of the token, hex-encoded.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Identifies one sign-in session; all tokens rotated from the same login share it.</summary>
    public Guid FamilyId { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
