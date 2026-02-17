using Vulscan.Application.DTOs.Auth;

namespace Vulscan.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<LoginResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<UserInfo> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
}
