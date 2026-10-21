using Microsoft.Extensions.Logging.Abstractions;
using Vulscan.Application.DTOs.Intel;
using Vulscan.Application.Services;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;
using Vulscan.Infrastructure.Data;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Reports;

/// <summary>
/// Where intelligence has been gathered for a CVE, reports show its real CVSS score, CISA KEV listing and EPSS next to
/// each finding. Findings from older scans carry a score that was estimated from a few vector flags; the real one
/// replaces it, and where nothing has been gathered yet the stored value is left alone.
/// </summary>
public class IntelOverlayTests
{
    private static readonly DateTime T0 = ScanSeeder.T0;

    private static ReportService Reports(VulscanDbContext db) => new(db, NullLogger<ReportService>.Instance);

    private static VulnerabilityIntel Intel(string id, double? cvss, bool kev = false, double? epss = null, string status = IntelStatus.Complete) => new()
    {
        AdvisoryId = id,
        SchemaVersion = VulnerabilityIntelDto.CurrentSchemaVersion,
        Status = status,
        CvssScore = cvss,
        KevListed = kev,
        KevDueDate = kev ? new DateTime(2021, 12, 24) : null,
        EpssScore = epss,
        EpssPercentile = epss is null ? null : 0.97,
        FetchedAt = T0,
        RefreshAfter = T0.AddDays(1),
    };

    private static async Task<(Project, ScanRun)> SeedAsync(VulscanDbContext db, params string[] cves)
    {
        var seed = new ScanSeeder(db);
        var project = await seed.ProjectAsync("Zajel");
        var repo = await seed.RepoAsync(project, "CTS");
        var scan = await seed.ScanAsync(project, ScanStatus.Completed, T0, repo, cves.Select(c => (c, VulnerabilitySeverity.High)).ToList());
        // The scanner of old stored an estimate: 9.0 for any high-impact network vector.
        foreach (var v in db.Vulnerabilities.Where(v => v.ScanRunId == scan.Id)) v.CvssScore = 9.0;
        await db.SaveChangesAsync();
        return (project, scan);
    }

    [Fact]
    public async Task Project_findings_show_the_real_score_kev_and_epss_where_known()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        var (project, _) = await SeedAsync(db, "CVE-2021-44228", "CVE-2015-8857", "CVE-2020-1111");
        db.VulnerabilityIntel.AddRange(
            Intel("CVE-2021-44228", 10.0, kev: true, epss: 0.99999),
            Intel("CVE-2015-8857", 6.5, epss: 0.03559));                   // CVE-2020-1111: nothing gathered yet
        await db.SaveChangesAsync();

        var findings = (await Reports(db).GetProjectReportAsync(project.Id))!.Repositories.Single().Vulnerabilities
            .ToDictionary(v => v.CveId);

        var log4shell = findings["CVE-2021-44228"];
        Assert.Equal(10.0, log4shell.CvssScore);
        Assert.True(log4shell.Kev);
        Assert.Equal(new DateTime(2021, 12, 24), log4shell.KevDueDate);
        Assert.Equal(0.99999, log4shell.Epss);

        var uglify = findings["CVE-2015-8857"];
        Assert.Equal(6.5, uglify.CvssScore);                                // the estimate of 9.0 is replaced
        Assert.False(uglify.Kev);
        Assert.Equal(0.03559, uglify.Epss);

        var unknown = findings["CVE-2020-1111"];
        Assert.Equal(9.0, unknown.CvssScore);                               // nothing better known: the stored value stays
        Assert.False(unknown.Kev);
        Assert.Null(unknown.Epss);
    }

    [Fact]
    public async Task Findings_are_ordered_by_the_real_score()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        var (project, _) = await SeedAsync(db, "CVE-2020-0001", "CVE-2020-0002", "CVE-2020-0003");
        db.VulnerabilityIntel.AddRange(Intel("CVE-2020-0001", 4.3), Intel("CVE-2020-0002", 9.8), Intel("CVE-2020-0003", 7.5));
        await db.SaveChangesAsync();

        var order = (await Reports(db).GetProjectReportAsync(project.Id))!.Repositories.Single().Vulnerabilities.Select(v => v.CveId);

        Assert.Equal(["CVE-2020-0002", "CVE-2020-0003", "CVE-2020-0001"], order);
    }

    [Fact]
    public async Task A_remembered_not_found_record_does_not_override_anything()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        var (project, _) = await SeedAsync(db, "CVE-2020-0001");
        db.VulnerabilityIntel.Add(Intel("CVE-2020-0001", null, status: IntelStatus.NotFound));
        await db.SaveChangesAsync();

        var finding = (await Reports(db).GetProjectReportAsync(project.Id))!.Repositories.Single().Vulnerabilities.Single();

        Assert.Equal(9.0, finding.CvssScore);
    }

    [Fact]
    public async Task The_cve_summaries_and_cve_detail_use_the_same_real_values()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        await SeedAsync(db, "CVE-2021-44228");
        db.VulnerabilityIntel.Add(Intel("CVE-2021-44228", 10.0, kev: true, epss: 0.99999));
        await db.SaveChangesAsync();

        var summary = Assert.Single(await Reports(db).GetVulnerabilitySummariesAsync());
        Assert.Equal(10.0, summary.CvssScore);
        Assert.True(summary.Kev);
        Assert.Equal(0.99999, summary.Epss);

        var detail = await Reports(db).GetVulnerabilityReportAsync("CVE-2021-44228");
        Assert.Equal(10.0, detail!.CvssScore);
    }

    [Fact]
    public async Task More_cves_than_one_query_batch_holds_are_all_looked_up()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        var ids = Enumerable.Range(1, 1200).Select(i => $"CVE-2020-{i:0000}").ToArray();   // SQL Server allows ~2100 parameters per query
        var (project, _) = await SeedAsync(db, ids);
        db.VulnerabilityIntel.AddRange(ids.Select(id => Intel(id, 5.0, kev: id.EndsWith('7'))));
        await db.SaveChangesAsync();

        var findings = (await Reports(db).GetProjectReportAsync(project.Id))!.Repositories.Single().Vulnerabilities;

        Assert.Equal(1200, findings.Count);
        Assert.All(findings, f => Assert.Equal(5.0, f.CvssScore));
        Assert.Equal(120, findings.Count(f => f.Kev));
    }
}
