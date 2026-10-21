using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vulscan.Application.DTOs.Auth;
using Vulscan.Domain.Enums;
using Vulscan.Infrastructure.BackgroundServices;
using Vulscan.Infrastructure.Data;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Auth;

public class DormantAccountSweeperTests
{
    private static DormantAccountSweeper Sweeper(AuthHarness h)
    {
        var services = new ServiceCollection();
        services.AddDbContext<VulscanDbContext>(o => o.UseSqlite(h.Db.Connection));
        services.AddSingleton<Vulscan.Application.Interfaces.IAuditService>(h.Audit);
        var provider = services.BuildServiceProvider();
        return new DormantAccountSweeper(provider.GetRequiredService<IServiceScopeFactory>(), h.Options, h.Time, NullLogger<DormantAccountSweeper>.Instance);
    }

    [Fact]
    public async Task Deactivates_accounts_idle_beyond_the_threshold_and_keeps_recent_ones()
    {
        using var h = new AuthHarness();
        await h.AddLocalUserAsync("stale");
        await h.AddLocalUserAsync("active");
        h.Time.Advance(TimeSpan.FromDays(80));
        await h.Auth.LoginAsync(new LoginRequest { Username = "active", Password = AuthHarness.AdminPassword });
        h.Time.Advance(TimeSpan.FromDays(20));       // stale: 100 idle days; active: 20

        var n = await Sweeper(h).SweepAsync(CancellationToken.None);

        Assert.True(n >= 1);
        Assert.False((await h.Context.Users.AsNoTracking().SingleAsync(u => u.Username == "stale")).IsActive);
        Assert.True((await h.Context.Users.AsNoTracking().SingleAsync(u => u.Username == "active")).IsActive);
        var evt = h.Audit.Of("User.AutoDeactivated").Single(e => e.Username == "stale");
        Assert.Contains("dormant", evt.Details);
    }

    [Fact]
    public async Task A_brand_new_account_that_has_never_signed_in_is_not_dormant_yet()
    {
        using var h = new AuthHarness();
        await h.AddLocalUserAsync("fresh");
        h.Time.Advance(TimeSpan.FromDays(30));

        await Sweeper(h).SweepAsync(CancellationToken.None);

        Assert.True((await h.Context.Users.AsNoTracking().SingleAsync(u => u.Username == "fresh")).IsActive);
    }

    [Fact]
    public async Task Never_deactivates_the_last_active_administrator()
    {
        using var h = new AuthHarness();
        var seeded = await h.Context.Users.SingleAsync(u => u.Username == "admin");   // the only admin, never signed in
        h.Time.Advance(TimeSpan.FromDays(400));
        Assert.Equal(UserRole.Admin, seeded.Role);

        await Sweeper(h).SweepAsync(CancellationToken.None);

        Assert.True((await h.Context.Users.AsNoTracking().SingleAsync(u => u.Username == "admin")).IsActive);
    }

    [Fact]
    public async Task A_reactivated_dormant_account_is_not_deactivated_again_on_the_next_sweep()
    {
        // Regression: reactivation must restart the dormancy clock, otherwise an old account is switched
        // off again within hours of an administrator turning it back on.
        using var h = new AuthHarness();
        var user = await h.AddLocalUserAsync("returning");
        var admin = await h.AddLocalUserAsync("boss", UserRole.Admin);
        h.CurrentUser.UserId = admin.Id;
        h.Time.Advance(TimeSpan.FromDays(120));
        await Sweeper(h).SweepAsync(CancellationToken.None);
        Assert.False((await h.Context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).IsActive);

        h.Context.ChangeTracker.Clear();   // the sweeper used its own context; drop our stale tracked copy (each real request gets a fresh one)
        await h.Users.ActivateAsync(user.Id);
        h.Time.Advance(TimeSpan.FromHours(7));
        await Sweeper(h).SweepAsync(CancellationToken.None);

        Assert.True((await h.Context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).IsActive);
    }

    [Fact]
    public async Task Revokes_the_sessions_of_accounts_it_deactivates()
    {
        using var h = new AuthHarness();
        await h.AddLocalUserAsync("sleeper");
        var login = await h.Auth.LoginAsync(new LoginRequest { Username = "sleeper", Password = AuthHarness.AdminPassword });
        h.Time.Advance(TimeSpan.FromDays(95));

        await Sweeper(h).SweepAsync(CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Auth.RefreshTokenAsync(login.RefreshToken));
    }
}
