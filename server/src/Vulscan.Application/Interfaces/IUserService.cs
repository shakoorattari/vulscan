using Vulscan.Application.DTOs.Users;

namespace Vulscan.Application.Interfaces;

/// <summary>
/// User access lifecycle for administrators: registration (see <see cref="IAuthService.RegisterAsync"/>), privilege
/// changes, periodic review, and removal of access.
/// </summary>
public interface IUserService
{
    Task<List<UserDto>> GetAllAsync(CancellationToken ct = default);
    Task<UserDto> SetRoleAsync(Guid id, string role, CancellationToken ct = default);
    Task<UserDto> DeactivateAsync(Guid id, string reason, CancellationToken ct = default);
    Task<UserDto> ActivateAsync(Guid id, CancellationToken ct = default);
    Task<UserDto> UnlockAsync(Guid id, CancellationToken ct = default);
    Task<UserDto> ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct = default);
    Task<UserDto> ReviewAsync(Guid id, string? note, CancellationToken ct = default);

    /// <summary>A point-in-time listing of every account, its role and review status, for access-review evidence.</summary>
    Task<string> ExportAccessReviewCsvAsync(CancellationToken ct = default);
}
