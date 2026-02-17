using Microsoft.EntityFrameworkCore;
using Vulscan.Application.DTOs.Auth;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;

namespace Vulscan.Application.Services;

public sealed class AuthService(
    DbContext dbContext,
    IJwtTokenService jwtTokenService) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await dbContext.Set<User>()
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive, ct)
            ?? throw new UnauthorizedAccessException("Invalid username or password.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid username or password.");

        user.LastLoginAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        var accessToken = jwtTokenService.GenerateAccessToken(user.Id, user.Username, user.Role.ToString());
        var refreshToken = jwtTokenService.GenerateRefreshToken();

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddHours(8),
            User = new UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role.ToString()
            }
        };
    }

    public Task<LoginResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        // In a production system, validate refresh token from a store.
        // For MVP, refresh tokens are stateless — validate and reissue.
        throw new NotImplementedException("Refresh token flow is available in a future release.");
    }

    public async Task<UserInfo> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var exists = await dbContext.Set<User>()
            .AnyAsync(u => u.Username == request.Username || u.Email == request.Email, ct);

        if (exists)
            throw new InvalidOperationException("A user with this username or email already exists.");

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            role = UserRole.Viewer;

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            Role = role,
            IsActive = true
        };

        dbContext.Set<User>().Add(user);
        await dbContext.SaveChangesAsync(ct);

        return new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role.ToString()
        };
    }
}
