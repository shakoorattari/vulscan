using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Enums;

namespace Vulscan.Tests.Http;

/// <summary>The real pipeline: middleware order, authorization, rate limiting, audit and security headers.</summary>
public class AuthPipelineTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    public AuthPipelineTests(ApiFactory factory) => _f = factory;

    [Fact]
    public async Task Public_config_reports_sign_in_methods_without_secrets()
    {
        var res = await _f.CreateClient().GetAsync("/api/v1/auth/config");
        var data = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("data");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(data.GetProperty("localEnabled").GetBoolean());
        Assert.True(data.GetProperty("entraEnabled").GetBoolean());
        Assert.Equal("22222222-2222-2222-2222-222222222222", data.GetProperty("clientId").GetString());
    }

    [Fact]
    public async Task Responses_carry_the_hardening_headers()
    {
        var res = await _f.CreateClient().GetAsync("/api/v1/health");

        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", res.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("default-src 'none'", res.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("no-store", res.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Protected_endpoints_reject_anonymous_callers()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().GetAsync("/api/v1/dashboard/summary")).StatusCode);

    [Fact]
    public async Task A_token_signed_with_the_wrong_key_is_rejected()
    {
        var client = _f.CreateClient();
        var good = await _f.LoginAsync("admin", "Vulscan@2025");
        var forged = good[..^6] + (good.EndsWith("AAAAAA") ? "BBBBBB" : "AAAAAA");   // corrupt the signature
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/dashboard/summary")).StatusCode);
    }

    [Fact]
    public async Task The_seeded_admin_is_confined_to_changing_its_password_until_it_does()
    {
        using var f = new ApiFactory();
        var client = await f.ClientForAsync("admin", "Vulscan@2025");

        // Everything except the password-change flow is refused — server side.
        var blocked = await client.GetAsync("/api/v1/dashboard/summary");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        var body = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("PASSWORD_CHANGE_REQUIRED", body.GetProperty("errors")[0].GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/audit-logs")).StatusCode);

        // Weak passwords are refused with the reason...
        var weak = await f.PostAsync(client, "/api/v1/auth/change-password", new { currentPassword = "Vulscan@2025", newPassword = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.Response.StatusCode);
        Assert.Contains("at least 12", weak.Body.GetProperty("message").GetString());

        // A mistyped current password is a 400 (bad field), never a 401 — that would sign the user out over a typo.
        var typo = await f.PostAsync(client, "/api/v1/auth/change-password", new { currentPassword = "Vulscan@2026", newPassword = "My-New-Strong-Passphrase-1!" });
        Assert.Equal(HttpStatusCode.BadRequest, typo.Response.StatusCode);

        // ...a good one unlocks the account and returns a clean token.
        var (res, ok) = await f.PostAsync(client, "/api/v1/auth/change-password",
            new { currentPassword = "Vulscan@2025", newPassword = "My-New-Strong-Passphrase-1!" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ok.GetProperty("data").GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/dashboard/summary")).StatusCode);

        // The old, published password no longer works.
        var (old, _) = await f.PostAsync(f.CreateClient(), "/api/v1/auth/login", new { username = "admin", password = "Vulscan@2025" });
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
    }

    [Fact]
    public async Task Logout_and_password_change_remain_reachable_while_a_change_is_required()
    {
        using var f = new ApiFactory();
        var (_, login) = await f.PostAsync(f.CreateClient(), "/api/v1/auth/login", new { username = "admin", password = "Vulscan@2025" });
        var refresh = login.GetProperty("data").GetProperty("refreshToken").GetString();
        var client = f.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("data").GetProperty("accessToken").GetString());

        Assert.Equal(HttpStatusCode.OK, (await f.PostAsync(client, "/api/v1/auth/logout", new { refreshToken = refresh })).Response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.PostAsync(f.CreateClient(), "/api/v1/auth/refresh", new { refreshToken = refresh })).Response.StatusCode);
    }

    [Fact]
    public async Task Roles_are_enforced_and_denials_are_audited()
    {
        using var f = new ApiFactory();
        await f.AddUserAsync("viewer1", UserRole.Viewer);
        await f.AddUserAsync("analyst1", UserRole.SecurityAnalyst);
        await f.AddUserAsync("boss", UserRole.Admin);

        var viewer = await f.ClientForAsync("viewer1");
        var analyst = await f.ClientForAsync("analyst1");
        var admin = await f.ClientForAsync("boss");

        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/v1/audit-logs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.GetAsync("/api/v1/audit-logs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/audit-logs")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v1/dashboard/summary")).StatusCode);   // viewers can still read

        var denied = await f.WithDbAsync(db => db.AuditLogs.Where(a => a.Action == "Access.Denied").Select(a => a.Username).ToListAsync());
        Assert.Contains("viewer1", denied);
        Assert.Contains("analyst1", denied);
    }

    [Fact]
    public async Task Mutations_and_exports_are_recorded_by_the_audit_filter_with_their_outcome()
    {
        using var f = new ApiFactory();
        await f.AddUserAsync("boss", UserRole.Admin);
        var admin = await f.ClientForAsync("boss");

        var patch = await admin.PatchAsync("/api/v1/vulnerabilities/99999999-9999-9999-9999-999999999999/status",
            JsonContent.Create(new { status = "Resolved" }));
        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/reports/vulnerabilities/export/csv")).StatusCode);

        var rows = await f.WithDbAsync(db => db.AuditLogs.Where(a => a.Username == "boss").ToListAsync());
        var failed = Assert.Single(rows, r => r.Action == "Vulnerabilities.UpdateStatus");
        Assert.Equal(AuditOutcome.Failure, failed.Outcome);
        Assert.Equal("99999999-9999-9999-9999-999999999999", failed.EntityId);
        Assert.Contains(rows, r => r.Action == "Reports.Export" && r.Outcome == AuditOutcome.Success);
        Assert.DoesNotContain(rows, r => r.Details?.Contains("Resolved") == true);   // request bodies are never recorded
    }

    [Fact]
    public async Task Failed_logins_are_audited_without_revealing_which_accounts_exist()
    {
        using var f = new ApiFactory();
        await f.AddUserAsync("real-user", UserRole.Viewer);

        var (r1, b1) = await f.PostAsync(f.CreateClient(), "/api/v1/auth/login", new { username = "real-user", password = "Wrong-Password-1!" });
        var (r2, b2) = await f.PostAsync(f.CreateClient(), "/api/v1/auth/login", new { username = "no-such-user", password = "Wrong-Password-1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, r2.StatusCode);
        Assert.Equal(b1.GetProperty("message").GetString(), b2.GetProperty("message").GetString());

        var failures = await f.WithDbAsync(db => db.AuditLogs.Where(a => a.Action == "Auth.LoginFailed").ToListAsync());
        Assert.Equal(2, failures.Count);
        Assert.All(failures, a => Assert.Equal(AuditOutcome.Failure, a.Outcome));
        Assert.DoesNotContain(failures, a => a.Details!.Contains("Wrong-Password"));          // the attempted password is never stored
    }

    [Fact]
    public async Task Account_lockout_works_end_to_end()
    {
        using var f = new ApiFactory();
        await f.AddUserAsync("target", UserRole.Viewer);
        for (var i = 0; i < 5; i++)
            await f.PostAsync(f.CreateClient(), "/api/v1/auth/login", new { username = "target", password = "Wrong-Password-1!" });

        var (res, _) = await f.PostAsync(f.CreateClient(), "/api/v1/auth/login", new { username = "target", password = ApiFactory.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);        // right password, but locked
        Assert.True(await f.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.Action == "Auth.AccountLocked")));
    }

    [Fact]
    public async Task Refresh_rotation_and_theft_detection_work_over_http()
    {
        using var f = new ApiFactory();
        await f.AddUserAsync("rotator", UserRole.Viewer);
        var (_, login) = await f.PostAsync(f.CreateClient(), "/api/v1/auth/login", new { username = "rotator", password = ApiFactory.Password });
        var first = login.GetProperty("data").GetProperty("refreshToken").GetString();

        var (ok, rotated) = await f.PostAsync(f.CreateClient(), "/api/v1/auth/refresh", new { refreshToken = first });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var second = rotated.GetProperty("data").GetProperty("refreshToken").GetString();
        Assert.NotEqual(first, second);

        // Backdate the rotation so the replay falls outside the double-submit grace window.
        await f.WithDbAsync(async db =>
        {
            foreach (var t in db.RefreshTokens.Where(t => t.RevokedReason == "rotated")) t.RevokedAt = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
            return 0;
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await f.PostAsync(f.CreateClient(), "/api/v1/auth/refresh", new { refreshToken = first })).Response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.PostAsync(f.CreateClient(), "/api/v1/auth/refresh", new { refreshToken = second })).Response.StatusCode);
        Assert.True(await f.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.Action == "Auth.RefreshReuseDetected")));
    }

    [Fact]
    public async Task Entra_exchange_issues_a_session_for_an_assigned_user_and_refuses_unassigned_ones()
    {
        using var f = new ApiFactory();
        f.Entra.Identity = new EntraIdentity("bbbbbbbb-0000-0000-0000-000000000001", "sec1@sdd.shj.ae", "sec1@sdd.shj.ae", "Sec One", ["Vulscan.SecurityAnalyst"]);

        var (res, body) = await f.PostAsync(f.CreateClient(), "/api/v1/auth/entra", new { idToken = "opaque" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Entra", body.GetProperty("data").GetProperty("user").GetProperty("authProvider").GetString());

        var client = f.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("data").GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/dashboard/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/users")).StatusCode);   // analyst, not admin

        f.Entra.Identity = new EntraIdentity("bbbbbbbb-0000-0000-0000-000000000002", "nobody@sdd.shj.ae", "nobody@sdd.shj.ae", "No Body", []);
        var (denied, _) = await f.PostAsync(f.CreateClient(), "/api/v1/auth/entra", new { idToken = "opaque" });
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        f.Entra.FailWith = "invalid signature";
        var (bad, badBody) = await f.PostAsync(f.CreateClient(), "/api/v1/auth/entra", new { idToken = "opaque" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Equal("Microsoft sign-in failed.", badBody.GetProperty("message").GetString());
    }

    [Fact]
    public async Task An_administrator_can_run_the_full_user_lifecycle_and_the_trail_shows_it()
    {
        using var f = new ApiFactory();
        await f.AddUserAsync("boss", UserRole.Admin);
        var admin = await f.ClientForAsync("boss");

        var (created, createdBody) = await f.PostAsync(admin, "/api/v1/users",
            new { username = "newhire", email = "newhire@sdd.test", password = "Temporary-Passphrase-77!", role = "Viewer" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = createdBody.GetProperty("data").GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/users/{id}/role", new { role = "SecurityAnalyst" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.PostAsync(admin, $"/api/v1/users/{id}/review", new { note = "ok" })).Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.PostAsync(admin, $"/api/v1/users/{id}/deactivate", new { reason = "contract ended" })).Response.StatusCode);

        var (login, _) = await f.PostAsync(f.CreateClient(), "/api/v1/auth/login", new { username = "newhire", password = "Temporary-Passphrase-77!" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        var csv = await admin.GetStringAsync("/api/v1/users/access-review/export/csv");
        Assert.Contains("newhire", csv);
        Assert.Contains("contract ended", csv);

        var actions = await f.WithDbAsync(db => db.AuditLogs.Select(a => a.Action).ToListAsync());
        foreach (var expected in new[] { "User.Created", "User.RoleChanged", "User.AccessReviewed", "User.Deactivated", "Users.Create", "Users.Deactivate" })
            Assert.Contains(expected, actions);
    }

    [Fact]
    public async Task The_last_administrator_cannot_be_removed_through_the_api()
    {
        using var f = new ApiFactory();
        await f.AddUserAsync("boss", UserRole.Admin);
        await f.WithDbAsync(async db => { (await db.Users.SingleAsync(u => u.Username == "admin")).IsActive = false; await db.SaveChangesAsync(); return 0; });
        var admin = await f.ClientForAsync("boss");
        var users = JsonDocument.Parse(await admin.GetStringAsync("/api/v1/users")).RootElement.GetProperty("data");
        var bossId = users.EnumerateArray().Single(u => u.GetProperty("username").GetString() == "boss").GetProperty("id").GetString();

        var demote = await admin.PutAsJsonAsync($"/api/v1/users/{bossId}/role", new { role = "Viewer" });

        Assert.Equal(HttpStatusCode.BadRequest, demote.StatusCode);
        Assert.Contains("last active administrator", await demote.Content.ReadAsStringAsync());
    }
}
