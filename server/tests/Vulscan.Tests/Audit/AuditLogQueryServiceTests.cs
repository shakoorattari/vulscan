using Vulscan.Application.DTOs.Audit;
using Vulscan.Application.Services;
using Vulscan.Domain.Entities;
using Vulscan.Infrastructure.Data;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Audit;

public class AuditLogQueryServiceTests
{
    private static async Task SeedAsync(VulscanDbContext db)
    {
        var t0 = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        db.AuditLogs.AddRange(
            new AuditLog { Timestamp = t0, Username = "alice", Action = "Auth.Login", EntityType = "Auth", Outcome = "Success", IpAddress = "10.0.0.5" },
            new AuditLog { Timestamp = t0.AddMinutes(1), Username = "mallory", Action = "Auth.Login", EntityType = "Auth", Outcome = "Failure", IpAddress = "10.0.0.9", Details = "{\"reason\":\"bad password\"}" },
            new AuditLog { Timestamp = t0.AddMinutes(2), Username = "alice", Action = "Projects.Delete", EntityType = "Projects", EntityId = "abc-123", Outcome = "Success" },
            new AuditLog { Timestamp = t0.AddMinutes(3), Username = "=cmd|'/c calc'!A1", Action = "Exceptions.Create", EntityType = "Exceptions", Outcome = "Success" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Returns_newest_first_with_paging_metadata()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        await SeedAsync(db);

        var page = await new AuditLogQueryService(db).GetPageAsync(new AuditLogFilterDto { Page = 1, PageSize = 3 });

        Assert.Equal(4, page.TotalCount);
        Assert.Equal(3, page.Items.Count);
        Assert.Equal("Exceptions.Create", page.Items[0].Action);
        Assert.Equal(2, page.TotalPages);
    }

    [Theory]
    [InlineData("Failure", null, null, 1)]
    [InlineData(null, "alice", null, 2)]
    [InlineData(null, null, "Login", 2)]
    [InlineData("Success", "alice", "Delete", 1)]
    public async Task Filters_combine(string? outcome, string? user, string? action, int expected)
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        await SeedAsync(db);

        var page = await new AuditLogQueryService(db).GetPageAsync(new AuditLogFilterDto { Outcome = outcome, Username = user, Action = action });

        Assert.Equal(expected, page.TotalCount);
    }

    [Fact]
    public async Task Free_text_search_covers_action_details_and_entity_id()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        await SeedAsync(db);
        var svc = new AuditLogQueryService(db);

        Assert.Equal(1, (await svc.GetPageAsync(new AuditLogFilterDto { Search = "bad password" })).TotalCount);
        Assert.Equal(1, (await svc.GetPageAsync(new AuditLogFilterDto { Search = "abc-123" })).TotalCount);
    }

    [Fact]
    public async Task Page_size_is_clamped_to_protect_the_database()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        await SeedAsync(db);

        var page = await new AuditLogQueryService(db).GetPageAsync(new AuditLogFilterDto { PageSize = 1_000_000 });

        Assert.Equal(200, page.PageSize);
    }

    [Fact]
    public async Task Csv_export_neutralises_formula_injection_from_usernames()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        await SeedAsync(db);

        var csv = await new AuditLogQueryService(db).ExportCsvAsync(new AuditLogFilterDto());

        Assert.StartsWith("Timestamp (UTC),User,Action", csv);
        Assert.DoesNotContain(",=cmd", csv);
        Assert.Contains("'=cmd", csv);
        Assert.Equal(5, csv.TrimEnd().Split('\n').Length);   // header + 4 rows
    }

    [Fact]
    public async Task Date_bounds_are_interpreted_as_utc_whatever_kind_they_arrive_as()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        await SeedAsync(db);   // events at 08:00, 08:01, 08:02, 08:03 UTC on 2026-10-01
        var svc = new AuditLogQueryService(db);
        var cut = new DateTime(2026, 10, 1, 8, 2, 0);

        // The same instant expressed three ways must select the same rows (events at/after 08:02 UTC => 2).
        var unspecified = await svc.GetPageAsync(new AuditLogFilterDto { From = DateTime.SpecifyKind(cut, DateTimeKind.Unspecified) });
        var utc = await svc.GetPageAsync(new AuditLogFilterDto { From = DateTime.SpecifyKind(cut, DateTimeKind.Utc) });
        var local = await svc.GetPageAsync(new AuditLogFilterDto { From = DateTime.SpecifyKind(cut, DateTimeKind.Utc).ToLocalTime() });

        Assert.Equal(2, unspecified.TotalCount);
        Assert.Equal(2, utc.TotalCount);
        Assert.Equal(2, local.TotalCount);
    }
}
