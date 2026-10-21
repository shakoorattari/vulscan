using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vulscan.Api.Controllers;
using Vulscan.Application.Common;
using Vulscan.Application.DTOs.Vulnerabilities;
using Vulscan.Application.Services;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;
using Vulscan.Infrastructure.Data;
using Vulscan.Infrastructure.Security;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Reports;

/// <summary>
/// Every scan is kept, but dashboards and reports show each project's current scan — its latest <i>completed</i>
/// one. A scan that is running, queued or failed must never replace it (no flash of zeros), and a project's scan is
/// never confused with another project's.
/// </summary>
public class CurrentScanTests
{
    private static readonly DateTime T0 = ScanSeeder.T0;

    /// <summary>
    /// Zajel: S1 completed (old), S2 completed (current), S3 running with rows already written.
    /// Smart SMTP: S4 completed (current — though older than Zajel's S2), S5 failed after it.
    /// Brand New: only a running first scan.
    /// </summary>
    private sealed record World(
        Project Zajel, Repository Cts, ScanRun S1, ScanRun S2, ScanRun S3,
        Project Smtp, Repository SmartSmtp, ScanRun S4, ScanRun S5,
        Project BrandNew, ScanRun First);

    private static async Task<World> SeedAsync(VulscanDbContext db)
    {
        var seed = new ScanSeeder(db);
        var zajel = await seed.ProjectAsync("Zajel");
        var cts = await seed.RepoAsync(zajel, "CTS");
        var s1 = await seed.ScanAsync(zajel, ScanStatus.Completed, T0, cts, ScanSeeder.Findings("old", critical: 1, high: 2), packages: 2);
        var s2 = await seed.ScanAsync(zajel, ScanStatus.Completed, T0.AddDays(1), cts, ScanSeeder.Findings("cur", critical: 2, high: 1, medium: 2), packages: 3);
        var s3 = await seed.ScanAsync(zajel, ScanStatus.Running, T0.AddDays(2), cts, ScanSeeder.Findings("partial", low: 1), packages: 1);

        var smtp = await seed.ProjectAsync("Smart SMTP");
        var smartSmtp = await seed.RepoAsync(smtp, "SmartSmtp");
        var s4 = await seed.ScanAsync(smtp, ScanStatus.Completed, T0.AddHours(12), smartSmtp, ScanSeeder.Findings("smtp", high: 1, low: 3), packages: 2);
        var s5 = await seed.ScanAsync(smtp, ScanStatus.Failed, T0.AddDays(3));

        var brandNew = await seed.ProjectAsync("Brand New");
        var first = await seed.ScanAsync(brandNew, ScanStatus.Running, T0.AddDays(3));
        return new World(zajel, cts, s1, s2, s3, smtp, smartSmtp, s4, s5, brandNew, first);
    }

    // ── checks (shared by the SQLite and SQL Server runs) ──────────────────────

    private static async Task CurrentIdsAsync(VulscanDbContext db, World w)
    {
        var ids = await CurrentScans.IdsAsync(db);

        // One per project that has a completed scan: not the running/failed ones, not the older completed one.
        Assert.Equal(new[] { w.S2.Id, w.S4.Id }.Order(), ids.Order());
        Assert.Equal(w.S2.Id, await CurrentScans.IdForProjectAsync(db, w.Zajel.Id));
        Assert.Equal(w.S4.Id, await CurrentScans.IdForProjectAsync(db, w.Smtp.Id));
        Assert.Null(await CurrentScans.IdForProjectAsync(db, w.BrandNew.Id));
        Assert.Equal(new[] { w.Zajel.Id, w.BrandNew.Id }.Order(), (await CurrentScans.ProjectsInProgressAsync(db)).Order());
        Assert.Equal([w.S1.Id], await CurrentScans.ResolveAsync(db, w.S1.Id));   // history is still addressable
    }

    private static async Task DashboardAsync(VulscanDbContext db, World w)
    {
        var dash = await new DashboardService(db).GetSummaryAsync();

        // S2 (5) + S4 (4). Not the 13 rows in the table, which include S1 (3) and the running scan's partial row.
        Assert.Equal(9, dash.TotalVulnerabilities);
        Assert.Equal(2, dash.CriticalCount);
        Assert.Equal(2, dash.HighCount);      // 1 in Zajel + 1 in Smart SMTP
        Assert.Equal(2, dash.MediumCount);
        Assert.Equal(3, dash.LowCount);       // the running scan's partial low finding is not counted
        Assert.Equal(2, dash.ScansInProgress);
        Assert.Equal(T0.AddDays(1), dash.ResultsAsOf);
        Assert.Equal(6, dash.TotalScans);     // history size, untouched

        // "Last scan" is the latest that has finished: Smart SMTP's failed one — not a running scan.
        Assert.Equal("Failed", dash.LastScanStatus);
        Assert.Equal(T0.AddDays(3), dash.LastScanDate);

        var top = dash.TopVulnerableRepos.First();
        Assert.Equal("CTS", top.RepositoryName);
        Assert.Equal(5, top.TotalVulnerabilities);   // current scan only
        Assert.Equal(2, top.CriticalCount);
    }

    private static async Task ReportsAsync(VulscanDbContext db, World w)
    {
        var reports = new ReportService(db, NullLogger<ReportService>.Instance);

        var summary = await reports.GetExecutiveSummaryAsync();
        Assert.Equal(9, summary.TotalVulnerabilities);
        Assert.Equal(2 + 3, summary.TotalPackages);       // S2 + S4 packages; S1 and the running scan's are excluded
        Assert.Equal(T0.AddDays(1), summary.LastScanDate);
        Assert.Equal(2, summary.ScansInProgress);

        // Smart SMTP's current scan is older than Zajel's. It must still be shown, not hidden behind Zajel's.
        var byName = summary.ProjectSummaries.ToDictionary(p => p.ProjectName);
        Assert.Equal(5, byName["Zajel"].TotalVulnerabilities);
        Assert.True(byName["Zajel"].ScanInProgress);
        Assert.Equal(T0.AddDays(1), byName["Zajel"].LastScanAt);
        Assert.Equal(4, byName["Smart SMTP"].TotalVulnerabilities);
        Assert.False(byName["Smart SMTP"].ScanInProgress);
        Assert.Equal(T0.AddHours(12), byName["Smart SMTP"].LastScanAt);
        Assert.Equal(0, byName["Brand New"].TotalVulnerabilities);
        Assert.Null(byName["Brand New"].LastScanAt);       // "no results yet", distinguishable from "clean"
        Assert.True(byName["Brand New"].ScanInProgress);

        // Asking for a particular (historical) scan still works.
        var historical = await reports.GetProjectSummariesAsync(w.S1.Id);
        Assert.Equal(3, historical.Single(p => p.ProjectName == "Zajel").TotalVulnerabilities);
        Assert.Equal(0, historical.Single(p => p.ProjectName == "Smart SMTP").TotalVulnerabilities);

        var detail = await reports.GetProjectReportAsync(w.Zajel.Id);
        Assert.True(detail!.ScanInProgress);
        Assert.Equal(T0.AddDays(1), detail.LastScanAt);
        Assert.Equal(5, detail.Repositories.Single().Vulnerabilities.Count);   // S2, not the running scan's single row

        var fresh = await reports.GetProjectReportAsync(w.BrandNew.Id);
        Assert.True(fresh!.ScanInProgress);
        Assert.Null(fresh.LastScanAt);

        Assert.Equal(9, (await reports.GetVulnerabilitySummariesAsync()).Count);   // distinct CVEs across S2 (5) and S4 (4)
    }

    private static async Task TrendsAsync(VulscanDbContext db, World w)
    {
        var reports = new ReportService(db, NullLogger<ReportService>.Instance);

        // One point per completed scan; each is the estate total as of then (every project's latest scan at that time).
        var trend = await reports.GetSeverityTrendsAsync();
        Assert.Equal([w.S1.Id, w.S4.Id, w.S2.Id], trend.Select(t => t.ScanId));
        Assert.Equal([3, 7, 9], trend.Select(t => t.Total));        // 3 | 3 + 4 | 5 + 4  — not 3, 4, 5 (raw scans)
        Assert.Equal(2, trend[^1].Critical);
        Assert.Equal([7, 9], (await reports.GetSeverityTrendsAsync(2)).Select(t => t.Total));
    }

    private static async Task ProjectsAsync(VulscanDbContext db, World w)
    {
        var vault = new CredentialVault(new DataProtectionSecretProtector(new EphemeralDataProtectionProvider()));
        var projects = (await new ProjectService(db, vault).GetAllAsync()).ToDictionary(p => p.Name);

        // Running scan: the badge says so, but the figures are the previous completed scan's — never "0 / Clean".
        var zajel = projects["Zajel"];
        Assert.Equal("Running", zajel.LastScanStatus);
        Assert.True(zajel.ScanInProgress);
        Assert.Equal(w.S2.Id, zajel.LastScanId);
        Assert.Equal(5, zajel.LastScanTotalVulnerabilities);
        Assert.Equal(2, zajel.LastScanCriticalCount);
        Assert.Equal(T0.AddDays(1), zajel.ResultsAsOf);
        Assert.Equal(5, zajel.TotalVulnerabilities);   // not summed over S1 + S2 + S3
        Assert.Equal(3, zajel.TotalScans);             // history count

        // Failed scan: shown as failed, with the last good results.
        var smtp = projects["Smart SMTP"];
        Assert.Equal("Failed", smtp.LastScanStatus);
        Assert.False(smtp.ScanInProgress);
        Assert.Equal(4, smtp.LastScanTotalVulnerabilities);
        Assert.Equal(T0.AddHours(12), smtp.ResultsAsOf);

        // First scan still running: no results yet. ResultsAsOf is how the UI tells this from "clean".
        var fresh = projects["Brand New"];
        Assert.Equal("Running", fresh.LastScanStatus);
        Assert.True(fresh.ScanInProgress);
        Assert.Null(fresh.ResultsAsOf);
        Assert.Null(fresh.LastScanId);
    }

    private static async Task VulnerabilitiesAndPackagesAsync(VulscanDbContext db, World w)
    {
        var vulns = new VulnerabilityService(db);
        Assert.Equal(9, (await vulns.GetVulnerabilitiesAsync(new VulnerabilityFilterDto { PageSize = 100 })).TotalCount);
        Assert.Equal(3, (await vulns.GetVulnerabilitiesAsync(new VulnerabilityFilterDto { ScanRunId = w.S1.Id })).TotalCount);   // history
        Assert.Equal(5, (await vulns.GetVulnerabilitiesAsync(new VulnerabilityFilterDto { ProjectId = w.Zajel.Id })).TotalCount);

        var packages = new PackagesController(db, NullLogger<PackagesController>.Instance);

        // No filters: each project's current scan.
        var all = await InventoryAsync(packages, new { });
        Assert.Equal(5, all.GetProperty("totalPackages").GetInt32());
        Assert.Equal(JsonValueKind.Null, all.GetProperty("scanRunId").ValueKind);   // spans several scans, so no single scan to export

        // One project: its current scan, reported back so the per-scan exports work.
        var zajel = await InventoryAsync(packages, new { projectId = w.Zajel.Id });
        Assert.Equal(3, zajel.GetProperty("totalPackages").GetInt32());
        Assert.Equal(w.S2.Id, zajel.GetProperty("scanRunId").GetGuid());

        var byRepo = await InventoryAsync(packages, new { repositoryId = w.Cts.Id });
        Assert.Equal(w.S2.Id, byRepo.GetProperty("scanRunId").GetGuid());

        // A project that never completed a scan has no inventory yet — nothing from its in-progress scan.
        var fresh = await InventoryAsync(packages, new { projectId = w.BrandNew.Id });
        Assert.Equal(0, fresh.GetProperty("totalPackages").GetInt32());

        // Asking for an older scan still returns it.
        Assert.Equal(2, (await InventoryAsync(packages, new { scanRunId = w.S1.Id })).GetProperty("totalPackages").GetInt32());

        var byRepoList = Assert.IsType<OkObjectResult>(await packages.GetPackagesByRepository(w.Cts.Id));
        Assert.Equal(3, Assert.IsAssignableFrom<IEnumerable<DiscoveredPackage>>(byRepoList.Value).Count());
    }

    private static async Task<JsonElement> InventoryAsync(PackagesController controller, object args)
    {
        var json = JsonSerializer.SerializeToElement(args);
        Guid? Id(string name) => json.TryGetProperty(name, out var v) ? v.GetGuid() : null;

        var result = await controller.GetPackageInventory(Id("scanRunId"), Id("repositoryId"), Id("projectId"));
        var ok = Assert.IsType<OkObjectResult>(result);
        return JsonSerializer.SerializeToElement(ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("data");
    }

    // ── SQLite ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Current_scan_is_each_projects_latest_completed_scan()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        await CurrentIdsAsync(db, await SeedAsync(db));
    }

    [Fact]
    public async Task Dashboard_shows_current_results_and_ignores_history_and_scans_in_progress()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        await DashboardAsync(db, await SeedAsync(db));
    }

    [Fact]
    public async Task Reports_show_current_results_per_project()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        await ReportsAsync(db, await SeedAsync(db));
    }

    [Fact]
    public async Task Trend_is_the_estate_over_time_built_from_the_whole_scan_history()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        await TrendsAsync(db, await SeedAsync(db));
    }

    [Fact]
    public async Task Project_list_keeps_previous_results_while_a_scan_runs_or_after_one_fails()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        await ProjectsAsync(db, await SeedAsync(db));
    }

    [Fact]
    public async Task Vulnerability_list_and_package_inventory_default_to_current_scans()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        await VulnerabilitiesAndPackagesAsync(db, await SeedAsync(db));
    }

    [Fact]
    public async Task Nothing_is_deleted_by_any_of_this()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        var world = await SeedAsync(db);
        var before = (await db.Vulnerabilities.CountAsync(), await db.DiscoveredPackages.CountAsync(), await db.ScanRuns.CountAsync());

        await DashboardAsync(db, world);
        await ReportsAsync(db, world);
        await ProjectsAsync(db, world);

        Assert.Equal(before, (await db.Vulnerabilities.CountAsync(), await db.DiscoveredPackages.CountAsync(), await db.ScanRuns.CountAsync()));
        Assert.Equal((3 + 5 + 1 + 4, 2 + 3 + 1 + 2, 6), before);
    }

    // ── SQL Server: the same checks, to prove the queries translate there too ─────

    [SqlServerFact]
    public async Task All_of_the_above_translates_and_holds_on_sql_server()
    {
        await using var scratch = new ScratchSqlDatabase();
        await using (var migrate = scratch.NewContext())
            await DatabaseInitializer.MigrateAsync(migrate, NullLogger.Instance);

        await using var db = scratch.NewContext();
        var world = await SeedAsync(db);

        await CurrentIdsAsync(db, world);
        await DashboardAsync(db, world);
        await ReportsAsync(db, world);
        await TrendsAsync(db, world);
        await ProjectsAsync(db, world);
        await VulnerabilitiesAndPackagesAsync(db, world);
    }
}
