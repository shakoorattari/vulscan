using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulscan.Application.Auth;
using Vulscan.Application.Common;
using Vulscan.Application.DTOs.Users;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;

namespace Vulscan.Application.Services;

public sealed class UserService(
    DbContext dbContext,
    IAuditService audit,
    ICurrentUserService currentUser,
    AuthOptions options,
    TimeProvider time) : IUserService
{
    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<List<UserDto>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await dbContext.Set<User>().AsNoTracking().OrderBy(u => u.Username).ToListAsync(ct);
        return [.. users.Select(ToDto)];
    }

    public async Task<UserDto> SetRoleAsync(Guid id, string role, CancellationToken ct = default)
    {
        if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var newRole))
            throw new InvalidOperationException($"Unknown role '{role}'.");

        var user = await FindAsync(id, ct);
        if (user.AuthProvider == AuthProvider.Entra)
            throw new InvalidOperationException("This account's role is managed by its Microsoft Entra app role assignment, not here.");

        if (user.Role == newRole) return ToDto(user);

        if (user.Role == UserRole.Admin && user.IsActive)
            await EnsureAnotherActiveAdminAsync(user.Id, "demote", ct);

        var old = user.Role;
        user.Role = newRole;
        await RevokeSessionsAsync(user.Id, "role changed", ct);   // old tokens carry the old role
        await dbContext.SaveChangesAsync(ct);

        await audit.LogAsync(new AuditEntry("User.RoleChanged", "User", user.Id.ToString(),
            Details: Json(new { user = user.Username, from = old.ToString(), to = newRole.ToString() })), ct);
        return ToDto(user);
    }

    public async Task<UserDto> DeactivateAsync(Guid id, string reason, CancellationToken ct = default)
    {
        var user = await FindAsync(id, ct);
        if (currentUser.UserId == user.Id)
            throw new InvalidOperationException("You cannot deactivate your own account.");
        if (!user.IsActive) return ToDto(user);

        if (user.Role == UserRole.Admin)
            await EnsureAnotherActiveAdminAsync(user.Id, "deactivate", ct);

        user.IsActive = false;
        user.DeactivatedAt = Now;
        user.DeactivationReason = reason.Trim();
        await RevokeSessionsAsync(user.Id, "account deactivated", ct);   // access ends now, not when the token expires
        await dbContext.SaveChangesAsync(ct);

        await audit.LogAsync(new AuditEntry("User.Deactivated", "User", user.Id.ToString(),
            Details: Json(new { user = user.Username, reason = user.DeactivationReason })), ct);
        return ToDto(user);
    }

    public async Task<UserDto> ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var user = await FindAsync(id, ct);
        if (user.IsActive) return ToDto(user);

        user.IsActive = true;
        user.DeactivatedAt = null;
        user.DeactivationReason = null;
        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        // Restarts the dormancy clock; otherwise the sweeper would deactivate the account again within hours.
        user.AccessGrantedAt = Now;
        await dbContext.SaveChangesAsync(ct);

        await audit.LogAsync(new AuditEntry("User.Activated", "User", user.Id.ToString(),
            Details: Json(new { user = user.Username })), ct);
        return ToDto(user);
    }

    public async Task<UserDto> UnlockAsync(Guid id, CancellationToken ct = default)
    {
        var user = await FindAsync(id, ct);
        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        await dbContext.SaveChangesAsync(ct);

        await audit.LogAsync(new AuditEntry("User.Unlocked", "User", user.Id.ToString(),
            Details: Json(new { user = user.Username })), ct);
        return ToDto(user);
    }

    public async Task<UserDto> ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct = default)
    {
        var user = await FindAsync(id, ct);
        if (user.AuthProvider != AuthProvider.Local)
            throw new InvalidOperationException("This account signs in with Microsoft; its password is managed in Entra ID.");

        var problems = PasswordPolicy.Validate(newPassword, user.Username, user.Email);
        if (problems.Count > 0)
            throw new InvalidOperationException("Password does not meet the policy: " + string.Join(" ", problems));

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
        user.MustChangePassword = true;        // the administrator now knows it, so the owner must replace it
        user.PasswordChangedAt = Now;
        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        await RevokeSessionsAsync(user.Id, "password reset", ct);
        await dbContext.SaveChangesAsync(ct);

        await audit.LogAsync(new AuditEntry("User.PasswordReset", "User", user.Id.ToString(),
            Details: Json(new { user = user.Username })), ct);
        return ToDto(user);
    }

    public async Task<UserDto> ReviewAsync(Guid id, string? note, CancellationToken ct = default)
    {
        var user = await FindAsync(id, ct);
        user.LastAccessReviewAt = Now;
        user.LastAccessReviewBy = currentUser.Username ?? "unknown";
        await dbContext.SaveChangesAsync(ct);

        await audit.LogAsync(new AuditEntry("User.AccessReviewed", "User", user.Id.ToString(),
            Details: Json(new { user = user.Username, role = user.Role.ToString(), active = user.IsActive, note })), ct);
        return ToDto(user);
    }

    public async Task<string> ExportAccessReviewCsvAsync(CancellationToken ct = default)
    {
        var users = await GetAllAsync(ct);
        var now = Now;

        var csv = new StringBuilder();
        csv.AppendLine("Username,Email,Provider,Role,Active,Created (UTC),Last sign-in (UTC),Days since sign-in,Last reviewed (UTC),Reviewed by,Review overdue,Deactivated (UTC),Deactivation reason,Exported (UTC)");
        foreach (var u in users)
        {
            csv.AppendLine(Csv.Row(u.Username, u.Email, u.AuthProvider, u.Role, u.IsActive ? "Yes" : "No", u.CreatedAt,
                u.LastLoginAt, u.LastLoginAt is { } l ? (int)(now - l).TotalDays : null,
                u.LastAccessReviewAt, u.LastAccessReviewBy, u.IsActive && u.ReviewOverdue ? "Yes" : "No",
                u.DeactivatedAt, u.DeactivationReason, now));
        }
        return csv.ToString();
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private async Task<User> FindAsync(Guid id, CancellationToken ct) =>
        await dbContext.Set<User>().FirstOrDefaultAsync(u => u.Id == id, ct)
        ?? throw new KeyNotFoundException("User not found.");

    private async Task EnsureAnotherActiveAdminAsync(Guid exceptUserId, string action, CancellationToken ct)
    {
        var others = await dbContext.Set<User>().CountAsync(u => u.Id != exceptUserId && u.IsActive && u.Role == UserRole.Admin, ct);
        if (others == 0)
            throw new InvalidOperationException($"Cannot {action} the last active administrator.");
    }

    private async Task RevokeSessionsAsync(Guid userId, string reason, CancellationToken ct)
    {
        foreach (var t in await dbContext.Set<RefreshToken>().Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct))
        {
            t.RevokedAt = Now;
            t.RevokedReason = reason;
        }
    }

    private UserDto ToDto(User u)
    {
        var now = Now;
        return new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            Role = u.Role.ToString(),
            AuthProvider = u.AuthProvider.ToString(),
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt,
            LockedUntil = u.LockoutEndAt is { } until && until > now ? until : null,
            MustChangePassword = u.MustChangePassword,
            LastAccessReviewAt = u.LastAccessReviewAt,
            LastAccessReviewBy = u.LastAccessReviewBy,
            ReviewOverdue = u.LastAccessReviewAt is null
                            || (now - u.LastAccessReviewAt.Value).TotalDays > options.AccessReviewIntervalDays,
            DeactivatedAt = u.DeactivatedAt,
            DeactivationReason = u.DeactivationReason,
        };
    }

    private static string Json(object value) => JsonSerializer.Serialize(value);
}
