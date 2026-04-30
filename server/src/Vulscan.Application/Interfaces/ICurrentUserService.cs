namespace Vulscan.Application.Interfaces;

/// <summary>
/// Provides current authenticated user context.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
}
