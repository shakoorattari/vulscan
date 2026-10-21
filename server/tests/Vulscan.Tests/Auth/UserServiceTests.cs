using Microsoft.EntityFrameworkCore;
using Vulscan.Application.DTOs.Auth;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Auth;

public class UserServiceTests
{
    private static async Task<(AuthHarness H, User Admin, User Analyst)> SetupAsync()
    {
        var h = new AuthHarness();
        var admin = await h.AddLocalUserAsync("root-admin", UserRole.Admin);
        var analyst = await h.AddLocalUserAsync("analyst", UserRole.SecurityAnalyst);
        h.CurrentUser.UserId = admin.Id;
        h.CurrentUser.Username = admin.Username;
        return (h, admin, analyst);
    }

    [Fact]
    public async Task Changing_a_role_ends_the_users_sessions_because_their_tokens_carry_the_old_role()
    {
        var (h, _, analyst) = await SetupAsync();
        using var _h = h;
        var login = await h.Auth.LoginAsync(new LoginRequest { Username = "analyst", Password = AuthHarness.AdminPassword });

        var dto = await h.Users.SetRoleAsync(analyst.Id, "Viewer");

        Assert.Equal("Viewer", dto.Role);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.RefreshTokenAsync(login.RefreshToken));
        var evt = Assert.Single(h.Audit.Of("User.RoleChanged"));
        Assert.Contains("SecurityAnalyst", evt.Details);
    }

    [Fact]
    public async Task Entra_user_roles_cannot_be_edited_locally()
    {
        var (h, _, _) = await SetupAsync();
        using var _h = h;
        var ent = new User { Username = "e@sdd.test", Email = "e@sdd.test", AuthProvider = AuthProvider.Entra, ExternalId = Guid.NewGuid().ToString(), Role = UserRole.Viewer };
        h.Context.Users.Add(ent);
        await h.Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Users.SetRoleAsync(ent.Id, "Admin"));
        Assert.Contains("Entra", ex.Message);
    }

    [Fact]
    public async Task The_last_active_admin_cannot_be_demoted_or_deactivated()
    {
        var (h, admin, _) = await SetupAsync();
        using var _h = h;
        h.CurrentUser.UserId = Guid.NewGuid();   // acting as someone else, so the self-deactivation rule isn't what blocks us

        // The seeded admin row exists in every database; take it out of play so root-admin really is the last one.
        (await h.Context.Users.SingleAsync(u => u.Username == "admin")).IsActive = false;
        await h.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Users.SetRoleAsync(admin.Id, "Viewer"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Users.DeactivateAsync(admin.Id, "test reason"));

        // With a second admin, the first can go.
        await h.AddLocalUserAsync("second-admin", UserRole.Admin);
        var dto = await h.Users.DeactivateAsync(admin.Id, "left the team");
        Assert.False(dto.IsActive);
    }

    [Fact]
    public async Task Seeded_admin_counts_as_an_admin_for_the_last_admin_guard()
    {
        // The seeded admin row exists in every database; guard logic must see it.
        var (h, admin, _) = await SetupAsync();
        using var _h = h;
        h.CurrentUser.UserId = Guid.NewGuid();

        var dto = await h.Users.DeactivateAsync(admin.Id, "replaced by the seeded admin");   // seeded admin is a second active admin
        Assert.False(dto.IsActive);
    }

    [Fact]
    public async Task You_cannot_deactivate_yourself()
    {
        var (h, admin, _) = await SetupAsync();
        using var _h = h;
        await h.AddLocalUserAsync("second-admin", UserRole.Admin);

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Users.DeactivateAsync(admin.Id, "oops"));
    }

    [Fact]
    public async Task Deactivation_records_who_why_and_ends_access_immediately()
    {
        var (h, _, analyst) = await SetupAsync();
        using var _h = h;
        var login = await h.Auth.LoginAsync(new LoginRequest { Username = "analyst", Password = AuthHarness.AdminPassword });

        var dto = await h.Users.DeactivateAsync(analyst.Id, "  Moved to another department  ");

        Assert.False(dto.IsActive);
        Assert.Equal("Moved to another department", dto.DeactivationReason);
        Assert.NotNull(dto.DeactivatedAt);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.RefreshTokenAsync(login.RefreshToken));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            h.Auth.LoginAsync(new LoginRequest { Username = "analyst", Password = AuthHarness.AdminPassword }));
        Assert.Single(h.Audit.Of("User.Deactivated"));
    }

    [Fact]
    public async Task Reactivation_restores_sign_in_clears_lockout_and_restarts_the_dormancy_clock()
    {
        var (h, _, analyst) = await SetupAsync();
        using var _h = h;
        await h.Users.DeactivateAsync(analyst.Id, "leave of absence");
        h.Time.Advance(TimeSpan.FromDays(200));

        var dto = await h.Users.ActivateAsync(analyst.Id);

        Assert.True(dto.IsActive);
        Assert.Null(dto.DeactivationReason);
        var row = await h.Context.Users.SingleAsync(u => u.Id == analyst.Id);
        Assert.Equal(h.Time.GetUtcNow().UtcDateTime, row.AccessGrantedAt);
        await h.Auth.LoginAsync(new LoginRequest { Username = "analyst", Password = AuthHarness.AdminPassword });
    }

    [Fact]
    public async Task Unlock_clears_a_lockout()
    {
        var (h, _, analyst) = await SetupAsync();
        using var _h = h;
        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.LoginAsync(new LoginRequest { Username = "analyst", Password = "wrong-password-123!" }));
        Assert.NotNull((await h.Users.GetAllAsync()).Single(u => u.Id == analyst.Id).LockedUntil);

        await h.Users.UnlockAsync(analyst.Id);

        Assert.Null((await h.Users.GetAllAsync()).Single(u => u.Id == analyst.Id).LockedUntil);
        await h.Auth.LoginAsync(new LoginRequest { Username = "analyst", Password = AuthHarness.AdminPassword });
    }

    [Fact]
    public async Task Admin_password_reset_enforces_policy_forces_a_change_and_ends_sessions()
    {
        var (h, _, analyst) = await SetupAsync();
        using var _h = h;
        var login = await h.Auth.LoginAsync(new LoginRequest { Username = "analyst", Password = AuthHarness.AdminPassword });

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Users.ResetPasswordAsync(analyst.Id, "weak"));
        var dto = await h.Users.ResetPasswordAsync(analyst.Id, "Temporary-Passphrase-77!");

        Assert.True(dto.MustChangePassword);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.RefreshTokenAsync(login.RefreshToken));
        var relogin = await h.Auth.LoginAsync(new LoginRequest { Username = "analyst", Password = "Temporary-Passphrase-77!" });
        Assert.True(relogin.MustChangePassword);
    }

    [Fact]
    public async Task Review_stamps_the_reviewer_and_the_overdue_flag_follows_the_configured_interval()
    {
        var (h, _, analyst) = await SetupAsync();
        using var _h = h;
        Assert.True((await h.Users.GetAllAsync()).Single(u => u.Id == analyst.Id).ReviewOverdue);   // never reviewed

        var dto = await h.Users.ReviewAsync(analyst.Id, "Confirmed with line manager");
        Assert.Equal("root-admin", dto.LastAccessReviewBy);
        Assert.False(dto.ReviewOverdue);

        h.Time.Advance(TimeSpan.FromDays(91));
        Assert.True((await h.Users.GetAllAsync()).Single(u => u.Id == analyst.Id).ReviewOverdue);
        Assert.Single(h.Audit.Of("User.AccessReviewed"));
    }

    [Fact]
    public async Task Access_review_export_lists_everyone_and_neutralises_formula_injection()
    {
        var (h, _, _) = await SetupAsync();
        using var _h = h;
        await h.AddLocalUserAsync("=HYPERLINK(\"http://evil\")", UserRole.Viewer);

        var csv = await h.Users.ExportAccessReviewCsvAsync();

        Assert.StartsWith("Username,Email,Provider,Role,Active", csv);
        Assert.Contains("root-admin", csv);
        Assert.Contains("analyst", csv);
        Assert.Contains("'=HYPERLINK", csv);
        Assert.DoesNotContain("\n=HYPERLINK", csv);
        Assert.DoesNotContain(",=HYPERLINK", csv);
    }
}
