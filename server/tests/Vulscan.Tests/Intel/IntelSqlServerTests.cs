using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vulscan.Application.DTOs.Intel;
using Vulscan.Application.Services;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;
using Vulscan.Infrastructure.Data;
using Vulscan.Infrastructure.Intel;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Intel;

/// <summary>
/// The refresher's queries, its parallel fetching and the report lookups against a real SQL Server (run with
/// <c>VULSCAN_TEST_SQL</c>). SQLite is more forgiving about query translation, parameter limits and concurrent
/// writers than production is.
/// </summary>
public class IntelSqlServerTests
{
    private static readonly DateTime T0 = ScanSeeder.T0;

    private static async Task<ScratchSqlDatabase> MigratedAsync()
    {
        var scratch = new ScratchSqlDatabase();
        await using var db = scratch.NewContext();
        await DatabaseInitializer.MigrateAsync(db, NullLogger.Instance);
        return scratch;
    }

    [SqlServerFact]
    public async Task Pending_advisories_are_found_with_the_same_rules_as_on_sqlite()
    {
        await using var scratch = await MigratedAsync();
        using var h = new IntelHarness(scratch.NewContext);
        await using var db = h.NewContext();
        var seed = new ScanSeeder(db);
        var p = await seed.ProjectAsync("Zajel");
        var repo = await seed.RepoAsync(p, "CTS");
        await seed.ScanAsync(p, ScanStatus.Completed, T0, repo, [("CVE-2020-0001", VulnerabilitySeverity.High)]);                 // history
        await seed.ScanAsync(p, ScanStatus.Completed, T0.AddDays(1), repo,
            [("CVE-2020-0002", VulnerabilitySeverity.High), ("CVE-2020-0003", VulnerabilitySeverity.High), ("CVE-2020-0004", VulnerabilitySeverity.High)]);
        await seed.ScanAsync(p, ScanStatus.Running, T0.AddDays(2), repo, [("CVE-2020-7777", VulnerabilitySeverity.High)]);

        var now = h.Time.GetUtcNow().UtcDateTime;
        db.VulnerabilityIntel.AddRange(
            new VulnerabilityIntel { AdvisoryId = "CVE-2020-0002", SchemaVersion = 1, Status = IntelStatus.Complete, FetchedAt = now, RefreshAfter = now.AddHours(5) },
            new VulnerabilityIntel { AdvisoryId = "CVE-2020-0003", SchemaVersion = 1, Status = IntelStatus.Complete, FetchedAt = now, RefreshAfter = now.AddMinutes(-1) });
        await db.SaveChangesAsync();

        var pending = await h.Refresher(db).FindPendingAsync(100);

        Assert.Equal(["CVE-2020-0003", "CVE-2020-0004"], pending.Order());
        Assert.Equal("CVE-2020-0004", (await h.Refresher(db).FindPendingAsync(1)).Single());        // unseen first, and the cap applies
    }

    [SqlServerFact]
    public async Task Several_advisories_are_fetched_in_parallel_and_stored_without_conflicts()
    {
        await using var scratch = await MigratedAsync();
        using var h = new IntelHarness(scratch.NewContext);
        var ids = Enumerable.Range(1, 40).Select(i => $"CVE-2020-{i:0000}").ToList();
        foreach (var id in ids)
            h.Osv.Records[id] = new Vulscan.Infrastructure.Clients.OsvVulnerability { Id = id, Summary = $"Summary {id}", Details = "Details." };

        await using var db = h.NewContext();
        var seed = new ScanSeeder(db);
        var p = await seed.ProjectAsync("Zajel");
        var repo = await seed.RepoAsync(p, "CTS");
        await seed.ScanAsync(p, ScanStatus.Completed, T0, repo, ids.Select(i => (i, VulnerabilitySeverity.High)).ToList());

        Assert.Equal(4, h.Options.Concurrency);                              // genuinely parallel: one context per fetch
        var fetched = await h.Refresher(db).RefreshPendingAsync();

        Assert.Equal(40, fetched);
        await using var verify = scratch.NewContext();
        Assert.Equal(40, await verify.VulnerabilityIntel.CountAsync(r => r.Status == IntelStatus.Complete));
        Assert.Equal(40, await verify.VulnerabilityIntel.Select(r => r.AdvisoryId).Distinct().CountAsync());
        Assert.Empty(await h.Refresher(db).FindPendingAsync(100));
    }

    [SqlServerFact]
    public async Task The_signal_sweep_pages_through_every_stored_cve()
    {
        await using var scratch = await MigratedAsync();
        using var h = new IntelHarness(scratch.NewContext);
        await using var db = h.NewContext();
        var now = h.Time.GetUtcNow().UtcDateTime;
        for (var i = 0; i < 450; i++)
        {
            var id = $"CVE-2020-{i:0000}";
            var doc = System.Text.Json.JsonSerializer.Serialize(
                new VulnerabilityIntelDto { Id = id, Sources = [new IntelSourceDto { Name = "EPSS", Status = "ok", FetchedAt = now }] },
                VulnerabilityIntelService.Json);
            db.VulnerabilityIntel.Add(new VulnerabilityIntel { AdvisoryId = id, SchemaVersion = 1, Status = IntelStatus.Complete, Document = doc, FetchedAt = now, RefreshAfter = now.AddHours(24) });
            if (i % 2 == 0) h.Kev.Entries[id] = FakeKev.Entry(id);
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var (kev, _) = await h.Refresher(db).SweepSignalsAsync();

        Assert.Equal(225, kev);
        await using var verify = scratch.NewContext();
        Assert.Equal(225, await verify.VulnerabilityIntel.CountAsync(r => r.KevListed));
    }

    [SqlServerFact]
    public async Task Reports_look_up_more_cves_than_a_single_sql_statement_could_hold()
    {
        await using var scratch = await MigratedAsync();
        await using var db = scratch.NewContext();
        var ids = Enumerable.Range(1, 2500).Select(i => $"CVE-2020-{i:0000}").ToArray();      // SQL Server allows about 2100 parameters
        var seed = new ScanSeeder(db);
        var p = await seed.ProjectAsync("Zajel");
        var repo = await seed.RepoAsync(p, "CTS");
        await seed.ScanAsync(p, ScanStatus.Completed, T0, repo, ids.Select(i => (i, VulnerabilitySeverity.High)).ToList());
        var now = T0;
        db.VulnerabilityIntel.AddRange(ids.Select(id => new VulnerabilityIntel
        {
            AdvisoryId = id, SchemaVersion = 1, Status = IntelStatus.Complete, CvssScore = 5.5, KevListed = id.EndsWith('7'), FetchedAt = now, RefreshAfter = now,
        }));
        await db.SaveChangesAsync();

        var findings = (await new ReportService(db, NullLogger<ReportService>.Instance).GetProjectReportAsync(p.Id))!
            .Repositories.Single().Vulnerabilities;

        Assert.Equal(2500, findings.Count);
        Assert.All(findings, f => Assert.Equal(5.5, f.CvssScore));
        Assert.Equal(250, findings.Count(f => f.Kev));
    }

    [SqlServerFact]
    public async Task The_unique_index_stops_two_records_for_one_advisory()
    {
        await using var scratch = await MigratedAsync();
        await using var db = scratch.NewContext();
        db.VulnerabilityIntel.Add(new VulnerabilityIntel { AdvisoryId = "CVE-2020-0001", SchemaVersion = 1, FetchedAt = T0, RefreshAfter = T0 });
        await db.SaveChangesAsync();

        await using var other = scratch.NewContext();
        other.VulnerabilityIntel.Add(new VulnerabilityIntel { AdvisoryId = "CVE-2020-0001", SchemaVersion = 1, FetchedAt = T0, RefreshAfter = T0 });

        await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
    }
}
