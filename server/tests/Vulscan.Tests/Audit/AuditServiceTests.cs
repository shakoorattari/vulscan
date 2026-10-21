using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vulscan.Application.Interfaces;
using Vulscan.Infrastructure.Data;
using Vulscan.Infrastructure.Services;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Audit;

public class AuditServiceTests
{
    private static (AuditService Service, ServiceProvider Provider, DefaultHttpContext Http) Build(TestDb test, bool authenticated)
    {
        var services = new ServiceCollection();
        services.AddDbContext<VulscanDbContext>(o => o.UseSqlite(test.Connection));
        var provider = services.BuildServiceProvider();

        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.1.2.3");
        if (authenticated)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "6c1f4a52-0000-0000-0000-000000000042"), new Claim(ClaimTypes.Name, "analyst1")],
                authenticationType: "test"));
        }

        var service = new AuditService(provider.GetRequiredService<IServiceScopeFactory>(), new HttpContextAccessor { HttpContext = http }, NullLogger<AuditService>.Instance);
        return (service, provider, http);
    }

    [Fact]
    public async Task Fills_user_and_ip_from_the_current_request()
    {
        using var test = new TestDb();
        var (service, provider, _) = Build(test, authenticated: true);
        using var _p = provider;

        await service.LogAsync(new AuditEntry("Projects.Delete", "Projects", "p-1"));

        await using var db = test.NewContext();
        var row = Assert.Single(db.AuditLogs);
        Assert.Equal("analyst1", row.Username);
        Assert.Equal(Guid.Parse("6c1f4a52-0000-0000-0000-000000000042"), row.UserId);
        Assert.Equal("10.1.2.3", row.IpAddress);
        Assert.Equal(AuditOutcome.Success, row.Outcome);
    }

    [Fact]
    public async Task Explicit_username_is_kept_for_unauthenticated_events_such_as_failed_logins()
    {
        using var test = new TestDb();
        var (service, provider, _) = Build(test, authenticated: false);
        using var _p = provider;

        await service.LogAsync(new AuditEntry("Auth.LoginFailed", "Auth", Username: "ghost", Outcome: AuditOutcome.Failure));

        await using var db = test.NewContext();
        var row = Assert.Single(db.AuditLogs);
        Assert.Equal("ghost", row.Username);
        Assert.Null(row.UserId);
        Assert.Equal(AuditOutcome.Failure, row.Outcome);
    }

    [Fact]
    public async Task Overlong_fields_are_truncated_not_rejected()
    {
        using var test = new TestDb();
        var (service, provider, _) = Build(test, authenticated: true);
        using var _p = provider;

        await service.LogAsync(new AuditEntry(new string('a', 500), "T", EntityId: new string('9', 500), Details: new string('d', 10_000)));

        await using var db = test.NewContext();
        var row = Assert.Single(db.AuditLogs);
        Assert.Equal(200, row.Action.Length);
        Assert.Equal(100, row.EntityId!.Length);
        Assert.Equal(4000, row.Details!.Length);
    }

    [Fact]
    public async Task A_broken_database_never_breaks_the_audited_operation()
    {
        using var test = new TestDb();
        var (service, provider, _) = Build(test, authenticated: true);
        using var _p = provider;
        test.Connection.Close();   // simulate the DB going away

        var ex = await Record.ExceptionAsync(() => service.LogAsync(new AuditEntry("x", "y")));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Write_survives_cancellation_of_the_request_token()
    {
        using var test = new TestDb();
        var (service, provider, _) = Build(test, authenticated: true);
        using var _p = provider;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await service.LogAsync(new AuditEntry("Export", "Reports"), cts.Token);

        await using var db = test.NewContext();
        Assert.Single(db.AuditLogs);
    }
}
