using Microsoft.EntityFrameworkCore;
using Vulscan.Application.DTOs.Intel;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;
using Vulscan.Infrastructure.Clients;
using Vulscan.Infrastructure.Intel;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Intel;

public class IntelRefresherTests
{
    private static readonly DateTime T0 = ScanSeeder.T0;

    private static IReadOnlyList<(string, VulnerabilitySeverity)> Cves(params string[] ids) =>
        ids.Select(id => (id, VulnerabilitySeverity.High)).ToList();

    // ── what needs fetching ────────────────────────────────────────────────────

    [Fact]
    public async Task Pending_are_the_cves_of_each_projects_current_scan_that_have_no_fresh_record()
    {
        using var h = new IntelHarness();
        await using var db = h.Db.NewContext();
        var seed = new ScanSeeder(db);
        var p = await seed.ProjectAsync("Zajel");
        var repo = await seed.RepoAsync(p, "CTS");
        await seed.ScanAsync(p, ScanStatus.Completed, T0, repo, Cves("CVE-2020-0001", "CVE-2020-9999"));      // history: ignored
        await seed.ScanAsync(p, ScanStatus.Completed, T0.AddDays(1), repo, Cves("CVE-2020-0002", "CVE-2020-0003", "CVE-2020-0004", "not a cve id"));
        await seed.ScanAsync(p, ScanStatus.Running, T0.AddDays(2), repo, Cves("CVE-2020-7777"));              // not current yet: ignored

        var now = h.Time.GetUtcNow().UtcDateTime;
        db.VulnerabilityIntel.AddRange(
            new VulnerabilityIntel { AdvisoryId = "CVE-2020-0002", SchemaVersion = VulnerabilityIntelDto.CurrentSchemaVersion, Status = IntelStatus.Complete, FetchedAt = now, RefreshAfter = now.AddHours(5) },   // fresh
            new VulnerabilityIntel { AdvisoryId = "CVE-2020-0003", SchemaVersion = VulnerabilityIntelDto.CurrentSchemaVersion, Status = IntelStatus.Complete, FetchedAt = now, RefreshAfter = now.AddMinutes(-1) }, // due
            new VulnerabilityIntel { AdvisoryId = "CVE-2020-0004", SchemaVersion = 0, Status = IntelStatus.Complete, FetchedAt = now, RefreshAfter = now.AddHours(5) });                                          // old format
        await db.SaveChangesAsync();

        var pending = await h.Refresher(db).FindPendingAsync(100);

        Assert.Equal(["CVE-2020-0003", "CVE-2020-0004"], pending.Order());     // "not a cve id" is dropped, fresh ones skipped
    }

    [Fact]
    public async Task Advisories_never_seen_come_before_ones_merely_due_for_a_refresh_and_the_cap_is_respected()
    {
        using var h = new IntelHarness();
        await using var db = h.Db.NewContext();
        var seed = new ScanSeeder(db);
        var p = await seed.ProjectAsync("Zajel");
        var repo = await seed.RepoAsync(p, "CTS");
        await seed.ScanAsync(p, ScanStatus.Completed, T0, repo, Cves("CVE-2020-0001", "CVE-2020-0002", "CVE-2020-0003", "CVE-2020-0004"));

        var now = h.Time.GetUtcNow().UtcDateTime;
        db.VulnerabilityIntel.AddRange(
            new VulnerabilityIntel { AdvisoryId = "CVE-2020-0001", SchemaVersion = 1, Status = IntelStatus.Complete, FetchedAt = now, RefreshAfter = now.AddMinutes(-5) },
            new VulnerabilityIntel { AdvisoryId = "CVE-2020-0002", SchemaVersion = 1, Status = IntelStatus.Complete, FetchedAt = now, RefreshAfter = now.AddMinutes(-5) });
        await db.SaveChangesAsync();

        var pending = await h.Refresher(db).FindPendingAsync(3);

        Assert.Equal(3, pending.Count);
        Assert.Equal(["CVE-2020-0003", "CVE-2020-0004"], pending.Take(2).Order());   // unseen first
    }

    [Fact]
    public async Task A_project_with_no_completed_scan_contributes_nothing()
    {
        using var h = new IntelHarness();
        await using var db = h.Db.NewContext();
        var seed = new ScanSeeder(db);
        var p = await seed.ProjectAsync("New");
        var repo = await seed.RepoAsync(p, "R");
        await seed.ScanAsync(p, ScanStatus.Running, T0, repo, Cves("CVE-2020-0001"));
        await seed.ScanAsync(p, ScanStatus.Failed, T0.AddDays(1), repo, Cves("CVE-2020-0002"));

        Assert.Empty(await h.Refresher(db).FindPendingAsync(100));
    }

    // ── fetching them ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_refresh_pass_fetches_every_pending_advisory_and_the_next_pass_finds_nothing_to_do()
    {
        using var h = new IntelHarness().WithUglifyJs();
        await using var db = h.Db.NewContext();
        var seed = new ScanSeeder(db);
        var p = await seed.ProjectAsync("Zajel");
        var repo = await seed.RepoAsync(p, "CTS");
        await seed.ScanAsync(p, ScanStatus.Completed, T0, repo, Cves("CVE-2015-8857", "CVE-2099-0001"));
        var refresher = h.Refresher(db);

        var first = await refresher.RefreshPendingAsync();

        Assert.Equal(2, first);
        var rows = (await h.Db.NewContext().VulnerabilityIntel.ToListAsync()).ToDictionary(r => r.AdvisoryId);
        Assert.Equal(IntelStatus.Complete, rows["CVE-2015-8857"].Status);
        Assert.Equal(IntelStatus.NotFound, rows["CVE-2099-0001"].Status);        // asked, remembered as unknown

        Assert.Equal(0, await refresher.RefreshPendingAsync());                  // nothing left until something is due
    }

    [Fact]
    public async Task Fetching_is_off_when_intel_is_disabled()
    {
        using var h = new IntelHarness().WithUglifyJs();
        h.Options.Enabled = false;
        await using var db = h.Db.NewContext();
        var seed = new ScanSeeder(db);
        var p = await seed.ProjectAsync("Zajel");
        var repo = await seed.RepoAsync(p, "CTS");
        await seed.ScanAsync(p, ScanStatus.Completed, T0, repo, Cves("CVE-2015-8857"));

        Assert.Equal(0, await h.Refresher(db).RefreshPendingAsync());
        Assert.Equal((0, 0), await h.Refresher(db).SweepSignalsAsync());
        Assert.Empty(h.Osv.Lookups);
    }

    // ── KEV and EPSS stay current on records that are not yet due for a full refresh ──

    private static async Task<IntelHarness> HarnessWithStoredRecordAsync()
    {
        var h = new IntelHarness().WithUglifyJs();
        await using var db = h.Db.NewContext();
        await h.Service(db).GetAsync("CVE-2015-8857");
        return h;
    }

    [Fact]
    public async Task A_cve_newly_added_to_kev_shows_as_exploited_without_waiting_for_the_daily_refresh()
    {
        using var h = await HarnessWithStoredRecordAsync();
        await using var db = h.Db.NewContext();
        Assert.False((await db.VulnerabilityIntel.SingleAsync()).KevListed);

        h.Time.Advance(TimeSpan.FromHours(2));                               // record is not due for 22 more hours
        h.Kev.Entries["CVE-2015-8857"] = FakeKev.Entry("CVE-2015-8857");
        var (kev, _) = await h.Refresher(db).SweepSignalsAsync();

        Assert.Equal(1, kev);
        var row = await db.VulnerabilityIntel.SingleAsync();
        Assert.True(row.KevListed);
        Assert.Equal(new DateTime(2021, 12, 10), row.KevDateAdded);
        Assert.Equal(new DateTime(2021, 12, 24), row.KevDueDate);

        var dto = await h.Service(db).GetAsync("CVE-2015-8857");
        Assert.Equal("Log4j2", dto!.Kev!.Product);
        Assert.Equal(9.8, dto.Score);                                        // the rest of the record is untouched
        Assert.Contains(dto.Cvss, c => c.Version == "2.0");
    }

    [Fact]
    public async Task A_cve_removed_from_kev_is_unlisted_but_an_unreachable_catalog_changes_nothing()
    {
        using var h = await HarnessWithStoredRecordAsync();
        await using var db = h.Db.NewContext();
        h.Kev.Entries["CVE-2015-8857"] = FakeKev.Entry("CVE-2015-8857");
        await h.Refresher(db).SweepSignalsAsync();
        Assert.True((await db.VulnerabilityIntel.AsNoTracking().SingleAsync()).KevListed);

        h.Kev.Down = true;
        h.Kev.Entries.Clear();
        Assert.Equal((0, 0), await h.Refresher(db).SweepSignalsAsync());     // cannot tell: keep what we know
        Assert.True((await db.VulnerabilityIntel.AsNoTracking().SingleAsync()).KevListed);

        h.Kev.Down = false;
        await h.Refresher(db).SweepSignalsAsync();
        Assert.False((await db.VulnerabilityIntel.AsNoTracking().SingleAsync()).KevListed);   // catalog reachable and it is gone
    }

    [Fact]
    public async Task Epss_scores_are_refreshed_daily_and_not_asked_for_again_within_the_day()
    {
        using var h = await HarnessWithStoredRecordAsync();
        await using var db = h.Db.NewContext();
        h.Epss.Calls.Clear();

        await h.Refresher(db).SweepSignalsAsync();
        Assert.Empty(h.Epss.Calls);                                          // fetched an hour ago: not asked again

        h.Time.Advance(TimeSpan.FromHours(21));
        h.Epss.Scores["CVE-2015-8857"] = new EpssScore(0.2, 0.9, new DateTime(2026, 10, 21));
        var (_, epss) = await h.Refresher(db).SweepSignalsAsync();

        Assert.Equal(1, epss);
        Assert.Single(h.Epss.Calls);
        var row = await db.VulnerabilityIntel.AsNoTracking().SingleAsync();
        Assert.Equal(0.2, row.EpssScore);
        Assert.Equal(new DateTime(2026, 10, 21), row.EpssDate);
        Assert.Equal(0.2, (await h.Service(db).GetAsync("CVE-2015-8857"))!.Epss!.Score);
    }

    [Fact]
    public async Task The_sweep_covers_every_stored_cve_in_pages()
    {
        using var h = new IntelHarness();
        await using var db = h.Db.NewContext();
        var now = h.Time.GetUtcNow().UtcDateTime;
        for (var i = 0; i < 450; i++)
        {
            var id = $"CVE-2020-{i:0000}";
            var doc = System.Text.Json.JsonSerializer.Serialize(
                new VulnerabilityIntelDto { Id = id, Sources = [new IntelSourceDto { Name = "EPSS", Status = "ok", FetchedAt = now }] },
                VulnerabilityIntelService.Json);
            db.VulnerabilityIntel.Add(new VulnerabilityIntel
            {
                AdvisoryId = id, SchemaVersion = 1, Status = IntelStatus.Complete, Document = doc, FetchedAt = now, RefreshAfter = now.AddHours(24),
            });
            if (i % 2 == 0) h.Kev.Entries[id] = FakeKev.Entry(id);
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var (kev, _) = await h.Refresher(db).SweepSignalsAsync();

        Assert.Equal(225, kev);
        Assert.Equal(225, await h.Db.NewContext().VulnerabilityIntel.CountAsync(r => r.KevListed));
    }
}
