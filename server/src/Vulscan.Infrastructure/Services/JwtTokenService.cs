using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Vulscan.Application.Interfaces;

namespace Vulscan.Infrastructure.Services;

/// <summary>
/// JWT token generation and validation using symmetric HMAC-SHA256.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        _secretKey = jwtSection["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured.");
        _issuer = jwtSection["Issuer"] ?? "VulscanApi";
        _audience = jwtSection["Audience"] ?? "VulscanDashboard";

        // Short-lived access tokens limit how long a stolen or de-authorised token stays useful; sessions are
        // kept alive by rotating refresh tokens instead. ExpirationHours is honoured for older configurations.
        _expirationMinutes =
            int.TryParse(jwtSection["ExpirationMinutes"], out var minutes) && minutes > 0 ? minutes
            : int.TryParse(jwtSection["ExpirationHours"], out var hours) && hours > 0 ? hours * 60
            : 30;

        RefreshTokenHours = int.TryParse(jwtSection["RefreshTokenHours"], out var refreshHours) && refreshHours > 0 ? refreshHours : 12;
    }

    public int RefreshTokenHours { get; }

    public AccessToken GenerateAccessToken(string userId, string username, string role, bool mustChangePassword = false)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };
        if (mustChangePassword)
            claims.Add(new Claim(IJwtTokenService.MustChangePasswordClaim, "true"));

        var expires = DateTime.UtcNow.AddMinutes(_expirationMinutes);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public (string userId, string username, string role)? ValidateToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var handler = new JwtSecurityTokenHandler();

            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);

            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "0";
            var username = principal.FindFirstValue(ClaimTypes.Name)
                ?? principal.FindFirstValue(JwtRegisteredClaimNames.UniqueName) ?? "";
            var role = principal.FindFirstValue(ClaimTypes.Role) ?? "Viewer";

            return (userId, username, role);
        }
        catch
        {
            return null;
        }
    }
}
