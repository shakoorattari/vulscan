using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulscan.Application.Auth;
using Vulscan.Application.DTOs.Auth;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;

namespace Vulscan.Application.Services;

/// <summary>
/// Sign-in, session and password management.
/// <para>
/// Failure responses are deliberately uniform ("Invalid username or password.") whatever the real reason —
/// unknown user, wrong password, locked or deactivated account — so the endpoint cannot be used to discover
/// which accounts exist. The precise reason is recorded in the audit trail instead.
/// </para>
/// </summary>
public sealed class AuthService(
    DbContext dbContext,
    IJwtTokenService jwtTokenService,
    IAuditService audit,
    IEntraTokenValidator entraValidator,
    AuthOptions options,
    TimeProvider time) : IAuthService
{
    private const string GenericFailure = "Invalid username or password.";

    // A rotated token presented again within this window is treated as a benign race (two tabs refreshing at once)
    // and merely rejected; after it, reuse is treated as theft and the whole session is revoked.
    private static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(10);

    // Verified against when the account does not exist, so response time doesn't reveal whether it does.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("not-a-real-password", workFactor: 12);

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public AuthConfigDto GetConfig() => new()
    {
        LocalEnabled = options.Local.Enabled,
        EntraEnabled = options.Entra.IsConfigured,
        TenantId = options.Entra.IsConfigured ? options.Entra.TenantId : null,
        ClientId = options.Entra.IsConfigured ? options.Entra.ClientId : null,
        Authority = options.Entra.IsConfigured ? options.Entra.Authority : null,
    };

    // ── Local sign-in ───────────────────────────────────────────────────

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken ct = default)
    {
        var username = request.Username.Trim();

        if (!options.Local.Enabled)
            throw await FailLoginAsync(username, "local sign-in is disabled", ipAddress, "Username/password sign-in is disabled. Use Microsoft sign-in.");

        var user = await dbContext.Set<User>().FirstOrDefaultAsync(u => u.Username == username, ct);

        if (user is null || user.AuthProvider != AuthProvider.Local || user.PasswordHash is null)
        {
            BCrypt.Net.BCrypt.Verify(request.Password, DummyHash);
            throw await FailLoginAsync(username, user is null ? "unknown user" : "not a local account", ipAddress);
        }

        if (!user.IsActive)
        {
            BCrypt.Net.BCrypt.Verify(request.Password, DummyHash);
            throw await FailLoginAsync(username, "account deactivated", ipAddress, userId: user.Id);
        }

        if (user.LockoutEndAt is { } lockedUntil && lockedUntil > Now)
        {
            BCrypt.Net.BCrypt.Verify(request.Password, DummyHash);
            throw await FailLoginAsync(username, $"account locked until {lockedUntil:O}", ipAddress, userId: user.Id);
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await RegisterFailedAttemptAsync(user, ipAddress, ct);
            throw await FailLoginAsync(username, "wrong password", ipAddress, userId: user.Id);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        user.LastLoginAt = Now;
        await dbContext.SaveChangesAsync(ct);

        await audit.LogAsync(new AuditEntry("Auth.Login", "Auth", user.Id.ToString(),
            Details: Json(new { provider = "Local", mustChangePassword = user.MustChangePassword }),
            UserId: user.Id, Username: user.Username, IpAddress: ipAddress), ct);

        return (await IssueTokensAsync(user, ipAddress, ct)).Response;
    }

    private async Task RegisterFailedAttemptAsync(User user, string? ipAddress, CancellationToken ct)
    {
        user.FailedLoginCount++;
        var lockedNow = user.FailedLoginCount >= Math.Max(1, options.Lockout.MaxFailedAttempts);
        if (lockedNow)
        {
            user.LockoutEndAt = Now.AddMinutes(options.Lockout.DurationMinutes);
            user.FailedLoginCount = 0;
        }
        await dbContext.SaveChangesAsync(ct);

        if (lockedNow)
        {
            await audit.LogAsync(new AuditEntry("Auth.AccountLocked", "Auth", user.Id.ToString(),
                Details: Json(new { until = user.LockoutEndAt, minutes = options.Lockout.DurationMinutes }),
                Outcome: AuditOutcome.Denied, UserId: user.Id, Username: user.Username, IpAddress: ipAddress), ct);
        }
    }

    private async Task<UnauthorizedAccessException> FailLoginAsync(
        string username, string reason, string? ipAddress, string message = GenericFailure, Guid? userId = null)
    {
        await audit.LogAsync(new AuditEntry("Auth.LoginFailed", "Auth", userId?.ToString(),
            Details: Json(new { reason }), Outcome: AuditOutcome.Failure, UserId: userId, Username: username, IpAddress: ipAddress));
        return new UnauthorizedAccessException(message);
    }

    // ── Microsoft Entra sign-in ─────────────────────────────────────────

    public async Task<LoginResponse> LoginWithEntraAsync(string idToken, string? ipAddress = null, CancellationToken ct = default)
    {
        if (!options.Entra.IsConfigured)
            throw new UnauthorizedAccessException("Microsoft sign-in is not enabled.");

        EntraIdentity identity;
        try
        {
            identity = await entraValidator.ValidateAsync(idToken, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            await audit.LogAsync(new AuditEntry("Auth.EntraLoginFailed", "Auth", Details: Json(new { reason = ex.Message }),
                Outcome: AuditOutcome.Failure, Username: "(unverified token)", IpAddress: ipAddress), ct);
            throw new UnauthorizedAccessException("Microsoft sign-in failed.");
        }

        var role = MapRole(identity.Roles);
        if (role is null)
        {
            await audit.LogAsync(new AuditEntry("Auth.EntraDenied", "Auth",
                Details: Json(new { reason = "no Vulscan app role assigned", roles = identity.Roles }),
                Outcome: AuditOutcome.Denied, Username: identity.Username, IpAddress: ipAddress), ct);
            throw new UnauthorizedAccessException("Your account has not been granted access to Vulscan. Contact the SDD security team.");
        }

        var user = await dbContext.Set<User>().FirstOrDefaultAsync(u => u.ExternalId == identity.ObjectId, ct);
        if (user is null)
        {
            // Never silently adopt an existing local account that merely shares a username/email: that would let
            // whoever controls the Entra attribute take over a local (possibly admin) account.
            var clash = await dbContext.Set<User>().AnyAsync(u => u.Username == identity.Username || u.Email == identity.Email, ct);
            if (clash)
            {
                await audit.LogAsync(new AuditEntry("Auth.EntraDenied", "Auth",
                    Details: Json(new { reason = "username or email already used by another account" }),
                    Outcome: AuditOutcome.Denied, Username: identity.Username, IpAddress: ipAddress), ct);
                throw new UnauthorizedAccessException(
                    "An account with this username or email already exists and is not linked to Microsoft sign-in. Ask an administrator.");
            }

            user = new User
            {
                Username = identity.Username,
                Email = identity.Email,
                PasswordHash = null,
                Role = role.Value,
                AuthProvider = AuthProvider.Entra,
                ExternalId = identity.ObjectId,
                IsActive = true,
                AccessGrantedAt = Now,
            };
            dbContext.Set<User>().Add(user);
            await dbContext.SaveChangesAsync(ct);

            await audit.LogAsync(new AuditEntry("User.Provisioned", "User", user.Id.ToString(),
                Details: Json(new { provider = "Entra", role = role.Value.ToString() }),
                UserId: user.Id, Username: user.Username, IpAddress: ipAddress), ct);
        }
        else
        {
            if (!user.IsActive)
            {
                await audit.LogAsync(new AuditEntry("Auth.EntraDenied", "Auth", user.Id.ToString(),
                    Details: Json(new { reason = "account deactivated in Vulscan" }),
                    Outcome: AuditOutcome.Denied, UserId: user.Id, Username: user.Username, IpAddress: ipAddress), ct);
                throw new UnauthorizedAccessException("This account has been deactivated.");
            }

            if (user.Role != role.Value)
            {
                await audit.LogAsync(new AuditEntry("User.RoleChanged", "User", user.Id.ToString(),
                    Details: Json(new { from = user.Role.ToString(), to = role.Value.ToString(), source = "Entra app role" }),
                    UserId: user.Id, Username: user.Username, IpAddress: ipAddress), ct);
                user.Role = role.Value;
            }
        }

        user.Email = identity.Email;
        user.LastLoginAt = Now;
        user.FailedLoginCount = 0;
        await dbContext.SaveChangesAsync(ct);

        await audit.LogAsync(new AuditEntry("Auth.Login", "Auth", user.Id.ToString(),
            Details: Json(new { provider = "Entra", role = user.Role.ToString() }),
            UserId: user.Id, Username: user.Username, IpAddress: ipAddress), ct);

        return (await IssueTokensAsync(user, ipAddress, ct)).Response;
    }

    private UserRole? MapRole(IEnumerable<string> entraRoles)
    {
        UserRole? best = null;
        foreach (var r in entraRoles)
        {
            if (options.Entra.RoleMapping.TryGetValue(r, out var mapped) && Enum.TryParse<UserRole>(mapped, true, out var role))
                if (best is null || role > best) best = role;   // highest privilege wins; enum order is ascending privilege
        }
        return best;
    }

    // ── Sessions ────────────────────────────────────────────────────────

    public async Task<LoginResponse> RefreshTokenAsync(string refreshToken, string? ipAddress = null, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var token = await dbContext.Set<RefreshToken>().Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null)
        {
            await audit.LogAsync(new AuditEntry("Auth.RefreshFailed", "Auth", Details: Json(new { reason = "unknown token" }),
                Outcome: AuditOutcome.Failure, Username: "(unknown)", IpAddress: ipAddress), ct);
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var user = token.User;

        if (token.RevokedAt is { } revokedAt)
        {
            if (token.RevokedReason != "rotated")
            {
                // Revoked for another reason (logout, password change, deactivation, or as fallout of an earlier theft
                // detection). Just refuse — flagging it again would make one incident look like several.
                await audit.LogAsync(new AuditEntry("Auth.RefreshFailed", "Auth", user.Id.ToString(),
                    Details: Json(new { reason = $"token already revoked ({token.RevokedReason})" }), Outcome: AuditOutcome.Failure,
                    UserId: user.Id, Username: user.Username, IpAddress: ipAddress), ct);
            }
            else if (Now - revokedAt >= RotationGrace)
            {
                // A token that was already exchanged for a new one is being presented again: someone holds a copy.
                await RevokeFamilyAsync(token.FamilyId, "reuse detected", ct);
                await audit.LogAsync(new AuditEntry("Auth.RefreshReuseDetected", "Auth", user.Id.ToString(),
                    Details: Json(new { familyId = token.FamilyId }), Outcome: AuditOutcome.Failure,
                    UserId: user.Id, Username: user.Username, IpAddress: ipAddress), ct);
            }
            // else: within the grace window — a benign double-submit (two tabs); reject quietly, keep the session.

            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        if (token.ExpiresAt <= Now)
        {
            await RevokeFamilyAsync(token.FamilyId, "expired", ct);
            throw new UnauthorizedAccessException("Session expired. Please sign in again.");
        }

        if (!user.IsActive)
        {
            await RevokeFamilyAsync(token.FamilyId, "account deactivated", ct);
            await audit.LogAsync(new AuditEntry("Auth.RefreshFailed", "Auth", user.Id.ToString(),
                Details: Json(new { reason = "account deactivated" }), Outcome: AuditOutcome.Denied,
                UserId: user.Id, Username: user.Username, IpAddress: ipAddress), ct);
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        // Rotate. The new token inherits the session's absolute expiry: refreshing never extends a session
        // beyond RefreshTokenHours from the original sign-in.
        var issued = await IssueTokensAsync(user, ipAddress, ct, token.FamilyId, token.ExpiresAt);
        token.RevokedAt = Now;
        token.RevokedReason = "rotated";
        token.ReplacedByTokenHash = issued.TokenHash;
        await dbContext.SaveChangesAsync(ct);

        return issued.Response;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            await audit.LogAsync(new AuditEntry("Auth.Logout", "Auth"), ct);
            return;
        }

        var hash = Hash(refreshToken);
        var token = await dbContext.Set<RefreshToken>().Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null) return;

        await RevokeFamilyAsync(token.FamilyId, "logout", ct);
        await audit.LogAsync(new AuditEntry("Auth.Logout", "Auth", token.UserId.ToString(),
            UserId: token.UserId, Username: token.User.Username), ct);
    }

    private async Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct)
    {
        var live = await dbContext.Set<RefreshToken>().Where(t => t.FamilyId == familyId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in live)
        {
            t.RevokedAt = Now;
            t.RevokedReason = reason;
        }
        if (live.Count > 0) await dbContext.SaveChangesAsync(ct);
    }

    private async Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct)
    {
        var live = await dbContext.Set<RefreshToken>().Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in live)
        {
            t.RevokedAt = Now;
            t.RevokedReason = reason;
        }
        if (live.Count > 0) await dbContext.SaveChangesAsync(ct);
    }

    private async Task<(LoginResponse Response, string TokenHash)> IssueTokensAsync(
        User user, string? ipAddress, CancellationToken ct, Guid? familyId = null, DateTime? expiresAt = null)
    {
        var access = jwtTokenService.GenerateAccessToken(user.Id.ToString(), user.Username, user.Role.ToString(), user.MustChangePassword);
        var raw = jwtTokenService.GenerateRefreshToken();
        var hash = Hash(raw);

        dbContext.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = hash,
            FamilyId = familyId ?? Guid.NewGuid(),
            ExpiresAt = expiresAt ?? Now.AddHours(jwtTokenService.RefreshTokenHours),
            CreatedByIp = ipAddress,
        });
        await dbContext.SaveChangesAsync(ct);

        return (new LoginResponse
        {
            AccessToken = access.Token,
            RefreshToken = raw,
            ExpiresAt = access.ExpiresAt,
            MustChangePassword = user.MustChangePassword,
            User = ToInfo(user),
        }, hash);
    }

    // ── Passwords ───────────────────────────────────────────────────────

    public async Task<LoginResponse> ChangePasswordAsync(
        Guid userId, ChangePasswordRequest request, string? ipAddress = null, CancellationToken ct = default)
    {
        var user = await dbContext.Set<User>().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("Invalid session.");

        if (user.AuthProvider != AuthProvider.Local || user.PasswordHash is null)
            throw new InvalidOperationException("This account signs in with Microsoft; its password is managed in Entra ID.");

        if (user.LockoutEndAt is { } until && until > Now)
            throw new UnauthorizedAccessException("Account is temporarily locked.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            await RegisterFailedAttemptAsync(user, ipAddress, ct);   // a stolen session must not get unlimited guesses
            await audit.LogAsync(new AuditEntry("Auth.PasswordChangeFailed", "Auth", user.Id.ToString(),
                Details: Json(new { reason = "wrong current password" }), Outcome: AuditOutcome.Failure,
                UserId: user.Id, Username: user.Username, IpAddress: ipAddress), ct);
            // 400, not 401: the session is valid, a field in the request is wrong. (A 401 here would make clients
            // conclude the session is dead and sign the user out over a typo.)
            throw new InvalidOperationException("Current password is incorrect.");
        }

        if (request.NewPassword == request.CurrentPassword)
            throw new InvalidOperationException("The new password must be different from the current one.");

        var problems = PasswordPolicy.Validate(request.NewPassword, user.Username, user.Email);
        if (problems.Count > 0)
            throw new InvalidOperationException(string.Join(" ", problems));

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.MustChangePassword = false;
        user.PasswordChangedAt = Now;
        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        await dbContext.SaveChangesAsync(ct);

        await RevokeAllForUserAsync(user.Id, "password changed", ct);   // end every other session

        await audit.LogAsync(new AuditEntry("Auth.PasswordChanged", "Auth", user.Id.ToString(),
            UserId: user.Id, Username: user.Username, IpAddress: ipAddress), ct);

        return (await IssueTokensAsync(user, ipAddress, ct)).Response;
    }

    public async Task<UserInfo> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var problems = PasswordPolicy.Validate(request.Password, request.Username, request.Email);
        if (problems.Count > 0)
            throw new InvalidOperationException("Password does not meet the policy: " + string.Join(" ", problems));

        var exists = await dbContext.Set<User>()
            .AnyAsync(u => u.Username == request.Username || u.Email == request.Email, ct);
        if (exists)
            throw new InvalidOperationException("A user with this username or email already exists.");

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            role = UserRole.Viewer;

        var user = new User
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            Role = role,
            IsActive = true,
            AuthProvider = AuthProvider.Local,
            // An administrator chose this password, so the person it is handed to must replace it.
            MustChangePassword = true,
            AccessGrantedAt = Now,
        };

        dbContext.Set<User>().Add(user);
        await dbContext.SaveChangesAsync(ct);

        await audit.LogAsync(new AuditEntry("User.Created", "User", user.Id.ToString(),
            Details: Json(new { username = user.Username, role = role.ToString(), provider = "Local" })), ct);

        return ToInfo(user);
    }

    // ── helpers ─────────────────────────────────────────────────────────

    internal static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static UserInfo ToInfo(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        Role = user.Role.ToString(),
        AuthProvider = user.AuthProvider.ToString(),
    };

    private static string Json(object value) => JsonSerializer.Serialize(value);
}
