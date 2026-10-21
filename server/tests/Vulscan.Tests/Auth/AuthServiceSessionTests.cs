using Microsoft.EntityFrameworkCore;
using Vulscan.Application.DTOs.Auth;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Auth;

public class AuthServiceSessionTests
{
    private static async Task<LoginResponse> SignInAsync(AuthHarness h, string user = "alice")
    {
        await h.AddLocalUserAsync(user);
        return await h.Auth.LoginAsync(new LoginRequest { Username = user, Password = AuthHarness.AdminPassword });
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_keeps_the_session_family()
    {
        using var h = new AuthHarness();
        var first = await SignInAsync(h);

        h.Time.Advance(TimeSpan.FromMinutes(31));
        var second = await h.Auth.RefreshTokenAsync(first.RefreshToken);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        var tokens = await h.Context.RefreshTokens.OrderBy(t => t.CreatedAt).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.Equal(tokens[0].FamilyId, tokens[1].FamilyId);
        Assert.Equal("rotated", tokens[0].RevokedReason);
        Assert.Null(tokens[1].RevokedAt);
    }

    [Fact]
    public async Task Refreshing_never_extends_the_session_beyond_its_absolute_lifetime()
    {
        using var h = new AuthHarness();
        var first = await SignInAsync(h);
        var originalExpiry = (await h.Context.RefreshTokens.SingleAsync()).ExpiresAt;

        h.Time.Advance(TimeSpan.FromHours(6));
        await h.Auth.RefreshTokenAsync(first.RefreshToken);

        Assert.All(await h.Context.RefreshTokens.ToListAsync(), t => Assert.Equal(originalExpiry, t.ExpiresAt));
    }

    [Fact]
    public async Task An_expired_session_cannot_be_refreshed()
    {
        using var h = new AuthHarness();
        var first = await SignInAsync(h);

        h.Time.Advance(TimeSpan.FromHours(13));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.RefreshTokenAsync(first.RefreshToken));
    }

    [Fact]
    public async Task Reusing_a_rotated_token_after_the_grace_window_is_treated_as_theft_and_kills_the_session()
    {
        using var h = new AuthHarness();
        var first = await SignInAsync(h);
        var second = await h.Auth.RefreshTokenAsync(first.RefreshToken);   // legitimate rotation

        h.Time.Advance(TimeSpan.FromMinutes(5));                           // attacker replays the stolen first token
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.RefreshTokenAsync(first.RefreshToken));

        // The legitimate holder's newest token is revoked too: the whole sign-in session is dead.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.RefreshTokenAsync(second.RefreshToken));
        Assert.Single(h.Audit.Of("Auth.RefreshReuseDetected"));
    }

    [Fact]
    public async Task A_near_simultaneous_double_refresh_is_rejected_without_killing_the_session()
    {
        using var h = new AuthHarness();
        var first = await SignInAsync(h);
        var second = await h.Auth.RefreshTokenAsync(first.RefreshToken);   // tab A wins the race

        h.Time.Advance(TimeSpan.FromSeconds(2));                           // tab B presents the same token a moment later
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.RefreshTokenAsync(first.RefreshToken));

        Assert.Empty(h.Audit.Of("Auth.RefreshReuseDetected"));
        var third = await h.Auth.RefreshTokenAsync(second.RefreshToken);   // the session survives
        Assert.NotEmpty(third.AccessToken);
    }

    [Fact]
    public async Task Unknown_refresh_tokens_are_rejected()
        => await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new AuthHarness().Auth.RefreshTokenAsync("not-a-token"));

    [Fact]
    public async Task Deactivating_a_user_ends_refresh_immediately()
    {
        using var h = new AuthHarness();
        var first = await SignInAsync(h);
        var user = await h.Context.Users.SingleAsync(u => u.Username == "alice");
        user.IsActive = false;
        await h.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.RefreshTokenAsync(first.RefreshToken));
    }

    [Fact]
    public async Task Logout_revokes_the_whole_session_and_is_audited()
    {
        using var h = new AuthHarness();
        var first = await SignInAsync(h);
        var second = await h.Auth.RefreshTokenAsync(first.RefreshToken);

        await h.Auth.LogoutAsync(second.RefreshToken);

        Assert.All(await h.Context.RefreshTokens.ToListAsync(), t => Assert.NotNull(t.RevokedAt));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.RefreshTokenAsync(second.RefreshToken));
        Assert.Single(h.Audit.Of("Auth.Logout"));
    }

    // ── password change ─────────────────────────────────────────────────

    [Fact]
    public async Task Changing_the_password_clears_the_forced_change_ends_other_sessions_and_issues_a_clean_token()
    {
        using var h = new AuthHarness();
        var user = await h.AddLocalUserAsync("frank", mustChange: true);
        var login = await h.Auth.LoginAsync(new LoginRequest { Username = "frank", Password = AuthHarness.AdminPassword });
        Assert.True(login.MustChangePassword);

        var changed = await h.Auth.ChangePasswordAsync(user.Id,
            new ChangePasswordRequest { CurrentPassword = AuthHarness.AdminPassword, NewPassword = "Tr1cky-New-Passphrase!" });

        Assert.False(changed.MustChangePassword);
        var claims = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(changed.AccessToken).Claims;
        Assert.DoesNotContain(claims, c => c.Type == "must_change_password");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.RefreshTokenAsync(login.RefreshToken));   // old session gone
        await h.Auth.LoginAsync(new LoginRequest { Username = "frank", Password = "Tr1cky-New-Passphrase!" });
        Assert.Single(h.Audit.Of("Auth.PasswordChanged"));
    }

    [Fact]
    public async Task Wrong_current_password_is_refused_and_counts_toward_lockout()
    {
        using var h = new AuthHarness();
        var user = await h.AddLocalUserAsync("gina");

        for (var i = 0; i < 5; i++)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Auth.ChangePasswordAsync(user.Id,
                new ChangePasswordRequest { CurrentPassword = "nope-nope-nope-1A!", NewPassword = "Tr1cky-New-Passphrase!" }));
            Assert.Contains("Current password is incorrect", ex.Message);
        }

        Assert.NotNull((await h.Context.Users.SingleAsync(u => u.Username == "gina")).LockoutEndAt);
    }

    [Theory]
    [InlineData("short1A!", "at least 12")]
    [InlineData("alllowercaseletters1!", "upper-case")]
    [InlineData("ALLUPPERCASELETTERS1!", "lower-case")]
    [InlineData("NoDigitsInHere-Really!", "digit")]
    [InlineData("NoSymbolsInHere1234Aa", "symbol")]
    [InlineData("Vulscan@2025", "too common")]   // exactly 12 chars: rejected for being the published default, not for length
    [InlineData("Xx-gina-Passphrase-1", "username")]
    public async Task Weak_new_passwords_are_refused_with_the_reason(string candidate, string expectedFragment)
    {
        using var h = new AuthHarness();
        var user = await h.AddLocalUserAsync("gina");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Auth.ChangePasswordAsync(user.Id,
            new ChangePasswordRequest { CurrentPassword = AuthHarness.AdminPassword, NewPassword = candidate }));

        Assert.Contains(expectedFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task New_password_must_differ_from_the_current_one()
    {
        using var h = new AuthHarness();
        var user = await h.AddLocalUserAsync("hank");

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Auth.ChangePasswordAsync(user.Id,
            new ChangePasswordRequest { CurrentPassword = AuthHarness.AdminPassword, NewPassword = AuthHarness.AdminPassword }));
    }

    [Fact]
    public async Task Register_enforces_the_policy_forces_a_first_change_and_rejects_duplicates()
    {
        using var h = new AuthHarness();

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Auth.RegisterAsync(
            new RegisterRequest { Username = "newuser", Email = "n@sdd.test", Password = "weak", Role = "Viewer" }));

        var info = await h.Auth.RegisterAsync(new RegisterRequest
        { Username = "newuser", Email = "n@sdd.test", Password = "An0ther-Strong-Passphrase!", Role = "SecurityAnalyst" });
        Assert.Equal("SecurityAnalyst", info.Role);
        Assert.True((await h.Context.Users.SingleAsync(u => u.Username == "newuser")).MustChangePassword);
        Assert.Single(h.Audit.Of("User.Created"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Auth.RegisterAsync(
            new RegisterRequest { Username = "newuser", Email = "other@sdd.test", Password = "An0ther-Strong-Passphrase!" }));
    }
}
