using Microsoft.EntityFrameworkCore;
using Vulscan.Application.DTOs.Auth;
using Vulscan.Domain.Enums;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Auth;

public class AuthServiceLoginTests
{
    private const string Generic = "Invalid username or password.";

    [Fact]
    public async Task Successful_login_issues_tokens_stores_only_a_hash_and_audits()
    {
        using var h = new AuthHarness();
        var user = await h.AddLocalUserAsync("alice");

        var result = await h.Auth.LoginAsync(new LoginRequest { Username = "alice", Password = AuthHarness.AdminPassword }, "10.0.0.5");

        Assert.False(string.IsNullOrEmpty(result.AccessToken));
        Assert.Equal("SecurityAnalyst", result.User.Role);
        // Access-token expiry uses the real clock on purpose: the bearer middleware validates it against the real clock.
        Assert.Equal(DateTime.UtcNow.AddMinutes(30), result.ExpiresAt, TimeSpan.FromSeconds(10));

        var stored = await h.Context.RefreshTokens.SingleAsync();
        Assert.NotEqual(result.RefreshToken, stored.TokenHash);                  // never stored raw
        Assert.Equal(64, stored.TokenHash.Length);                               // SHA-256 hex
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal("10.0.0.5", stored.CreatedByIp);

        var audit = Assert.Single(h.Audit.Of("Auth.Login"));
        Assert.Equal("alice", audit.Username);
    }

    [Theory]
    [InlineData("unknown-user", AuthHarness.AdminPassword)]
    [InlineData("alice", "wrong-password-123!")]
    public async Task Unknown_user_and_wrong_password_are_indistinguishable(string username, string password)
    {
        using var h = new AuthHarness();
        await h.AddLocalUserAsync("alice");

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            h.Auth.LoginAsync(new LoginRequest { Username = username, Password = password }));

        Assert.Equal(Generic, ex.Message);
        Assert.Single(h.Audit.Of("Auth.LoginFailed"));
    }

    [Fact]
    public async Task Deactivated_account_gets_the_same_generic_message_but_a_specific_audit_reason()
    {
        using var h = new AuthHarness();
        var user = await h.AddLocalUserAsync("bob");
        user.IsActive = false;
        await h.Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            h.Auth.LoginAsync(new LoginRequest { Username = "bob", Password = AuthHarness.AdminPassword }));

        Assert.Equal(Generic, ex.Message);
        Assert.Contains("deactivated", h.Audit.Of("Auth.LoginFailed").Single().Details);
    }

    [Fact]
    public async Task Account_locks_after_repeated_failures_even_the_right_password_is_then_refused_until_it_expires()
    {
        using var h = new AuthHarness();
        await h.AddLocalUserAsync("carol");
        var bad = new LoginRequest { Username = "carol", Password = "wrong-password-123!" };
        var good = new LoginRequest { Username = "carol", Password = AuthHarness.AdminPassword };

        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.LoginAsync(bad));

        var locked = await h.Context.Users.SingleAsync(u => u.Username == "carol");
        Assert.NotNull(locked.LockoutEndAt);
        Assert.Single(h.Audit.Of("Auth.AccountLocked"));

        // Correct password while locked: refused, with the same generic message.
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.LoginAsync(good));
        Assert.Equal(Generic, ex.Message);

        // After the lockout window it works again and the counter is clear.
        h.Time.Advance(TimeSpan.FromMinutes(16));
        var ok = await h.Auth.LoginAsync(good);
        Assert.NotEmpty(ok.AccessToken);
        var after = await h.Context.Users.SingleAsync(u => u.Username == "carol");
        Assert.Null(after.LockoutEndAt);
        Assert.Equal(0, after.FailedLoginCount);
    }

    [Fact]
    public async Task A_successful_login_resets_the_failure_counter()
    {
        using var h = new AuthHarness();
        await h.AddLocalUserAsync("dave");
        for (var i = 0; i < 4; i++)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.LoginAsync(new LoginRequest { Username = "dave", Password = "wrong-password-123!" }));

        await h.Auth.LoginAsync(new LoginRequest { Username = "dave", Password = AuthHarness.AdminPassword });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.LoginAsync(new LoginRequest { Username = "dave", Password = "wrong-password-123!" }));

        var user = await h.Context.Users.SingleAsync(u => u.Username == "dave");
        Assert.Null(user.LockoutEndAt);                    // 4 fails + success + 1 fail must not lock
        Assert.Equal(1, user.FailedLoginCount);
    }

    [Fact]
    public async Task Entra_accounts_cannot_sign_in_with_a_password()
    {
        using var h = new AuthHarness();
        h.Context.Users.Add(new Vulscan.Domain.Entities.User
        {
            Username = "ent@sdd.test", Email = "ent@sdd.test", AuthProvider = AuthProvider.Entra,
            ExternalId = Guid.NewGuid().ToString(), PasswordHash = BCrypt.Net.BCrypt.HashPassword("x", 4),
        });
        await h.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            h.Auth.LoginAsync(new LoginRequest { Username = "ent@sdd.test", Password = "x" }));
    }

    [Fact]
    public async Task Local_sign_in_can_be_switched_off()
    {
        using var h = new AuthHarness(o => o.Local.Enabled = false);
        await h.AddLocalUserAsync("erin");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            h.Auth.LoginAsync(new LoginRequest { Username = "erin", Password = AuthHarness.AdminPassword }));
        Assert.False(h.Auth.GetConfig().LocalEnabled);
    }

    [Fact]
    public async Task Seeded_admin_must_change_the_published_default_password()
    {
        using var h = new AuthHarness();   // EnsureCreated seeds the admin row
        var result = await h.Auth.LoginAsync(new LoginRequest { Username = "admin", Password = "Vulscan@2025" });

        Assert.True(result.MustChangePassword);
        var claims = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken).Claims;
        Assert.Contains(claims, c => c.Type == "must_change_password" && c.Value == "true");
    }

    [Fact]
    public async Task Config_hides_entra_ids_until_entra_is_properly_configured()
    {
        using var disabled = new AuthHarness(o => o.Entra.Enabled = false);
        var cfg = disabled.Auth.GetConfig();
        Assert.False(cfg.EntraEnabled);
        Assert.Null(cfg.TenantId);
        Assert.Null(cfg.ClientId);

        using var halfConfigured = new AuthHarness(o => o.Entra.ClientId = "");
        Assert.False(halfConfigured.Auth.GetConfig().EntraEnabled);

        using var ready = new AuthHarness();
        var on = ready.Auth.GetConfig();
        Assert.True(on.EntraEnabled);
        Assert.Equal("22222222-2222-2222-2222-222222222222", on.ClientId);
        Assert.StartsWith("https://login.microsoftonline.com/11111111", on.Authority);
        await Task.CompletedTask;
    }
}
