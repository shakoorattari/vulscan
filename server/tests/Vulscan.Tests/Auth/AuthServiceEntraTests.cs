using Microsoft.EntityFrameworkCore;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Enums;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Auth;

public class AuthServiceEntraTests
{
    private static readonly string Oid = "aaaaaaaa-0000-0000-0000-000000000001";

    private static EntraIdentity Identity(string oid, string upn, params string[] roles) =>
        new(oid, upn, upn, "Test Person", roles);

    [Fact]
    public async Task First_sign_in_provisions_the_user_with_the_mapped_role_and_audits_it()
    {
        using var h = new AuthHarness();
        h.Entra.Identity = Identity(Oid, "analyst@sdd.shj.ae", "Vulscan.SecurityAnalyst");

        var result = await h.Auth.LoginWithEntraAsync("any-token", "10.1.1.1");

        Assert.Equal("SecurityAnalyst", result.User.Role);
        Assert.Equal("Entra", result.User.AuthProvider);
        Assert.False(result.MustChangePassword);

        var user = await h.Context.Users.SingleAsync(u => u.ExternalId == Oid);
        Assert.Equal(AuthProvider.Entra, user.AuthProvider);
        Assert.Null(user.PasswordHash);               // Vulscan holds no secret for Entra users
        Assert.NotNull(user.AccessGrantedAt);
        Assert.Single(h.Audit.Of("User.Provisioned"));
        Assert.Single(h.Audit.Of("Auth.Login"));
    }

    [Fact]
    public async Task The_highest_privilege_role_wins_when_several_are_assigned()
    {
        using var h = new AuthHarness();
        h.Entra.Identity = Identity(Oid, "multi@sdd.shj.ae", "Vulscan.Viewer", "Vulscan.Admin", "Vulscan.SecurityAnalyst");

        var result = await h.Auth.LoginWithEntraAsync("t");

        Assert.Equal("Admin", result.User.Role);
    }

    [Fact]
    public async Task A_user_with_no_vulscan_app_role_is_refused_and_not_provisioned()
    {
        using var h = new AuthHarness();
        h.Entra.Identity = Identity(Oid, "outsider@sdd.shj.ae", "SomeOtherApp.Role");

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.LoginWithEntraAsync("t"));

        Assert.Contains("not been granted access", ex.Message);
        Assert.Empty(h.Context.Users.Where(u => u.ExternalId == Oid));
        var denied = Assert.Single(h.Audit.Of("Auth.EntraDenied"));
        Assert.Equal(AuditOutcome.Denied, denied.Outcome);
    }

    [Fact]
    public async Task A_user_with_no_roles_at_all_is_refused()
    {
        using var h = new AuthHarness();
        h.Entra.Identity = Identity(Oid, "noroles@sdd.shj.ae");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.LoginWithEntraAsync("t"));
    }

    [Fact]
    public async Task Role_changes_in_entra_are_synced_at_next_sign_in_and_audited()
    {
        using var h = new AuthHarness();
        h.Entra.Identity = Identity(Oid, "u@sdd.shj.ae", "Vulscan.Viewer");
        await h.Auth.LoginWithEntraAsync("t");

        h.Entra.Identity = Identity(Oid, "u@sdd.shj.ae", "Vulscan.Admin");
        var result = await h.Auth.LoginWithEntraAsync("t");

        Assert.Equal("Admin", result.User.Role);
        Assert.Equal(1, await h.Context.Users.CountAsync(u => u.ExternalId == Oid));   // matched by object id, not duplicated
        var change = Assert.Single(h.Audit.Of("User.RoleChanged"));
        Assert.Contains("Viewer", change.Details);
        Assert.Contains("Admin", change.Details);
    }

    [Fact]
    public async Task A_deactivated_vulscan_account_stays_locked_out_even_with_a_valid_entra_token()
    {
        using var h = new AuthHarness();
        h.Entra.Identity = Identity(Oid, "u@sdd.shj.ae", "Vulscan.Admin");
        await h.Auth.LoginWithEntraAsync("t");
        var user = await h.Context.Users.SingleAsync(u => u.ExternalId == Oid);
        user.IsActive = false;
        await h.Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.LoginWithEntraAsync("t"));
        Assert.Contains("deactivated", ex.Message);
    }

    [Fact]
    public async Task An_entra_identity_never_takes_over_an_existing_local_account_with_the_same_name_or_email()
    {
        using var h = new AuthHarness();
        await h.AddLocalUserAsync("admin2", UserRole.Admin);       // email admin2@sdd.test
        h.Entra.Identity = new EntraIdentity(Oid, "admin2", "admin2@sdd.test", "Impostor", ["Vulscan.Admin"]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.LoginWithEntraAsync("t"));

        var local = await h.Context.Users.SingleAsync(u => u.Username == "admin2");
        Assert.Equal(AuthProvider.Local, local.AuthProvider);
        Assert.Null(local.ExternalId);
    }

    [Fact]
    public async Task Token_validation_failure_gives_a_generic_message_and_an_audit_record()
    {
        using var h = new AuthHarness();
        h.Entra.FailWith = "IDX10223: Lifetime validation failed. The token is expired.";

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.LoginWithEntraAsync("t"));

        Assert.Equal("Microsoft sign-in failed.", ex.Message);                 // no validator internals leak to the client
        Assert.Contains("expired", h.Audit.Of("Auth.EntraLoginFailed").Single().Details);
    }

    [Fact]
    public async Task Entra_sign_in_is_refused_when_not_enabled()
    {
        using var h = new AuthHarness(o => o.Entra.Enabled = false);
        h.Entra.Identity = Identity(Oid, "u@sdd.shj.ae", "Vulscan.Admin");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.LoginWithEntraAsync("t"));
    }

    [Fact]
    public async Task Entra_users_cannot_change_a_password_they_do_not_have()
    {
        using var h = new AuthHarness();
        h.Entra.Identity = Identity(Oid, "u@sdd.shj.ae", "Vulscan.Admin");
        var login = await h.Auth.LoginWithEntraAsync("t");

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Auth.ChangePasswordAsync(login.User.Id,
            new Vulscan.Application.DTOs.Auth.ChangePasswordRequest { CurrentPassword = "x", NewPassword = "Tr1cky-New-Passphrase!" }));
    }
}
