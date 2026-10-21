using Vulscan.Application.DTOs.Auth;

namespace Vulscan.Application.Interfaces;

public interface IAuthService
{
    AuthConfigDto GetConfig();

    Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken ct = default);

    /// <summary>Signs in with a Microsoft Entra ID token, provisioning the user on first sign-in.</summary>
    Task<LoginResponse> LoginWithEntraAsync(string idToken, string? ipAddress = null, CancellationToken ct = default);

    /// <summary>Rotates a refresh token. Presenting an already-used token revokes the whole session.</summary>
    Task<LoginResponse> RefreshTokenAsync(string refreshToken, string? ipAddress = null, CancellationToken ct = default);

    Task LogoutAsync(string? refreshToken, CancellationToken ct = default);

    Task<LoginResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? ipAddress = null, CancellationToken ct = default);

    Task<UserInfo> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
}
