namespace Vulscan.Application.Interfaces;

/// <summary>
/// JWT token generation and validation service abstraction.
/// </summary>
public interface IJwtTokenService
{
    string GenerateAccessToken(string userId, string username, string role);
    string GenerateRefreshToken();
    (string userId, string username, string role)? ValidateToken(string token);
}
