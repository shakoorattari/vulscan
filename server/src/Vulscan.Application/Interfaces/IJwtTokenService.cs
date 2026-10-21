namespace Vulscan.Application.Interfaces;

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public interface IJwtTokenService
{
    /// <summary>Claim present on tokens issued to a user who must change their password before using anything else.</summary>
    public const string MustChangePasswordClaim = "must_change_password";

    AccessToken GenerateAccessToken(string userId, string username, string role, bool mustChangePassword = false);

    /// <summary>A cryptographically random opaque token. Only its hash should ever be stored.</summary>
    string GenerateRefreshToken();

    /// <summary>Absolute lifetime of a sign-in session, i.e. how long refresh tokens can keep renewing it.</summary>
    int RefreshTokenHours { get; }

    (string userId, string username, string role)? ValidateToken(string token);
}
