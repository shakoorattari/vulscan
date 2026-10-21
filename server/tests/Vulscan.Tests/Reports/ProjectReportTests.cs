using Microsoft.Extensions.Logging.Abstractions;
using Vulscan.Application.Services;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;
using Vulscan.Infrastructure.Data;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Reports;

public class ProjectReportTests
{
    private static readonly DateTime T0 = new(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

    private static ReportService Service(VulscanDbContext db) => new(db, NullLogger<ReportService>.Instance);

    private sealed record Seeded(Project Project, Repository Repo, ScanRun Scan);

    /// <summary>One project with one repository and one completed scan finishing at <paramref name="completedAt"/>.</summary>
    private static async Task<Seeded> SeedProjectAsync(VulscanDbContext db, string name, DateTime completedAt)
    {
        var instance = db.Set<AzureDevOpsInstance>().FirstOrDefault() ?? new AzureDevOpsInstance { Name = "ado", Url = "https://ado", Collection = "c" };
        if (instance.Id == Guid.Empty) db.Add(instance);

        var project = new Project { InstanceId = instance.Id, Name = name, AzureProjectId = name, Url = $"https://ado/{name}" };
        db.Add(project);

        var repo = new Repository { ProjectId = project.Id, Name = $"{name}-repo", CloneUrl = $"https://ado/{name}/repo" };
        db.Add(repo);

        var scan = new ScanRun { ProjectId = project.Id, Status = ScanStatus.Completed, StartedAt = completedAt.AddMinutes(-5), CompletedAt = completedAt };
        db.Add(scan);

        await db.SaveChangesAsync();
        return new Seeded(project, repo, scan);
    }

    private static Vulnerability Vuln(Seeded s, string cve, string package, string version, VulnerabilitySeverity sev, double? cvss, string? fix = null) => new()
    {
        ScanRunId = s.Scan.Id,
        RepositoryId = s.Repo.Id,
        BranchName = "main",
        CveId = cve,
        PackageName = package,
        InstalledVersion = version,
        FixedVersion = fix,
        Severity = sev,
        CvssScore = cvss,
        FirstDetectedAt = T0,
    };

    private static DiscoveredPackage Pkg(Seeded s, string name, string version, string file, string ecosystem = "nuget") => new()
    {
        ScanRunId = s.Scan.Id,
        RepositoryId = s.Repo.Id,
        Ecosystem = ecosystem,
        Name = name,
        Version = version,
        SourceFile = file,
    };

    [Fact]
    public async Task Uses_the_projects_own_latest_scan_not_the_latest_scan_of_any_project()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();

        var older = await SeedProjectAsync(db, "Smart SMTP", T0);
        var newer = await SeedProjectAsync(db, "Zajel", T0.AddHours(1)); // globally the most recent scan
        db.AddRange(
            Vuln(older, "CVE-2024-0001", "left-pad", "1.0.0", VulnerabilitySeverity.High, 7.5),
            Vuln(newer, "CVE-2024-0002", "right-pad", "2.0.0", VulnerabilitySeverity.Low, 2.0));
        await db.SaveChangesAsync();

        var report = await Service(db).GetProjectReportAsync(older.Project.Id);

        Assert.NotNull(report);
        var finding = Assert.Single(Assert.Single(report!.Repositories).Vulnerabilities);
        Assert.Equal("CVE-2024-0001", finding.CveId);
        Assert.Equal(T0, report.LastScanAt);
    }

    [Fact]
    public async Task Project_without_a_completed_scan_reports_empty_with_no_scan_date()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        var seeded = await SeedProjectAsync(db, "Never scanned", T0);
        seeded.Scan.Status = ScanStatus.Failed;
        await db.SaveChangesAsync();

        var report = await Service(db).GetProjectReportAsync(seeded.Project.Id);

        Assert.NotNull(report);
        Assert.Null(report!.LastScanAt);
        Assert.Equal(0, report.TotalVulnerabilities);
        Assert.Empty(Assert.Single(report.Repositories).Vulnerabilities);
    }

    [Fact]
    public async Task Same_cve_package_and_version_in_several_manifests_is_one_finding_with_its_locations()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        var s = await SeedProjectAsync(db, "Zajel", T0);

        db.AddRange(
            // AutoMapper 12.0.1 is declared in two projects, so the scanner recorded the finding twice.
            Vuln(s, "CVE-2026-32933", "AutoMapper", "12.0.1", VulnerabilitySeverity.High, 7.5),
            Vuln(s, "CVE-2026-32933", "AutoMapper", "12.0.1", VulnerabilitySeverity.High, 7.5, fix: "16.1.1"),
            Vuln(s, "CVE-2024-0056", "Microsoft.Data.SqlClient", "5.0.1", VulnerabilitySeverity.High, 9.8, fix: "5.1.3"),
            // A different version of the same package is a different finding.
            Vuln(s, "CVE-2026-32933", "AutoMapper", "13.0.0", VulnerabilitySeverity.High, 7.5),
            Pkg(s, "AutoMapper", "12.0.1", "src/Api/Api.csproj"),
            Pkg(s, "AutoMapper", "12.0.1", "src/Worker/Worker.csproj"),
            Pkg(s, "AutoMapper", "12.0.1", "src/Worker/Worker.csproj"), // listed twice in one file
            Pkg(s, "AutoMapper", "13.0.0", "src/Legacy/Legacy.csproj"));
        await db.SaveChangesAsync();

        var report = await Service(db).GetProjectReportAsync(s.Project.Id);

        var repo = Assert.Single(report!.Repositories);
        Assert.Equal(3, repo.Vulnerabilities.Count);
        Assert.Equal(4, report.TotalVulnerabilities);   // raw rows, as the Reports list and dashboard count them
        Assert.Equal(3, report.UniqueVulnerabilities);

        var automapper = repo.Vulnerabilities.Single(v => v.PackageName == "AutoMapper" && v.InstalledVersion == "12.0.1");
        Assert.Equal(2, automapper.Occurrences);
        Assert.Equal("16.1.1", automapper.FixedVersion);   // a fix known from either row is kept
        Assert.Equal("nuget", automapper.Ecosystem);
        Assert.Equal(["src/Api/Api.csproj", "src/Worker/Worker.csproj"], automapper.SourceFiles);

        var legacy = repo.Vulnerabilities.Single(v => v.InstalledVersion == "13.0.0");
        Assert.Equal(1, legacy.Occurrences);
        Assert.Equal(["src/Legacy/Legacy.csproj"], legacy.SourceFiles);
    }

    [Fact]
    public async Task Findings_are_ordered_by_cvss_then_cve_and_missing_scores_sort_last()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();
        var s = await SeedProjectAsync(db, "Zajel", T0);
        db.AddRange(
            Vuln(s, "CVE-2024-0003", "c", "1.0.0", VulnerabilitySeverity.Low, null),
            Vuln(s, "CVE-2024-0002", "b", "1.0.0", VulnerabilitySeverity.High, 7.5),
            Vuln(s, "CVE-2024-0001", "a", "1.0.0", VulnerabilitySeverity.High, 7.5),
            Vuln(s, "CVE-2024-0004", "d", "1.0.0", VulnerabilitySeverity.Critical, 9.8));
        await db.SaveChangesAsync();

        var report = await Service(db).GetProjectReportAsync(s.Project.Id);

        Assert.Equal(
            ["CVE-2024-0004", "CVE-2024-0001", "CVE-2024-0002", "CVE-2024-0003"],
            report!.Repositories.Single().Vulnerabilities.Select(v => v.CveId));
    }

    [Fact]
    public async Task Unknown_project_returns_null()
    {
        using var test = new TestDb();
        await using var db = test.NewContext();

        Assert.Null(await Service(db).GetProjectReportAsync(Guid.NewGuid()));
    }
}
