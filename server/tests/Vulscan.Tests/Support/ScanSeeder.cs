using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;
using Vulscan.Infrastructure.Data;

namespace Vulscan.Tests.Support;

/// <summary>Builds projects, repositories and scans the way the scan processor leaves them.</summary>
public sealed class ScanSeeder(VulscanDbContext db)
{
    public static readonly DateTime T0 = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private AzureDevOpsInstance? _instance;

    public async Task<Project> ProjectAsync(string name)
    {
        if (_instance is null)
        {
            _instance = new AzureDevOpsInstance { Name = "ado", Url = "https://ado.example.test", Collection = "c" };
            db.Add(_instance);
            await db.SaveChangesAsync();
        }

        var project = new Project { InstanceId = _instance.Id, Name = name, AzureProjectId = name, Url = $"https://ado.example.test/{name}" };
        db.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    public async Task<Repository> RepoAsync(Project project, string name)
    {
        var repo = new Repository { ProjectId = project.Id, Name = name, CloneUrl = $"https://ado.example.test/{project.Name}/{name}" };
        db.Add(repo);
        await db.SaveChangesAsync();
        return repo;
    }

    /// <summary>
    /// One scan. Like the real processor, a <i>completed</i> scan gets its severity counters at the end, while a
    /// running or failed scan keeps zeros in them even if rows were already written.
    /// </summary>
    public async Task<ScanRun> ScanAsync(
        Project project, ScanStatus status, DateTime at, Repository? repo = null,
        IReadOnlyList<(string Cve, VulnerabilitySeverity Severity)>? vulns = null, int packages = 0)
    {
        var inProgress = status is ScanStatus.Queued or ScanStatus.Running;
        var scan = new ScanRun
        {
            ProjectId = project.Id,
            Status = status,
            StartedAt = at.AddMinutes(-5),
            CompletedAt = inProgress ? null : at,
        };
        db.Add(scan);
        await db.SaveChangesAsync();

        vulns ??= [];
        foreach (var (cve, severity) in vulns)
        {
            db.Add(new Vulnerability
            {
                ScanRunId = scan.Id,
                RepositoryId = repo!.Id,
                BranchName = "main",
                CveId = cve,
                PackageName = $"pkg-{cve}",
                InstalledVersion = "1.0.0",
                Severity = severity,
                FirstDetectedAt = at,
            });
        }

        for (var i = 0; i < packages; i++)
        {
            db.Add(new DiscoveredPackage
            {
                ScanRunId = scan.Id,
                RepositoryId = repo!.Id,
                Ecosystem = "npm",
                Name = $"lib-{i}",
                Version = "1.0.0",
                SourceFile = "package.json",
            });
        }

        if (status == ScanStatus.Completed)
        {
            scan.TotalVulnerabilities = vulns.Count;
            scan.CriticalCount = vulns.Count(v => v.Severity == VulnerabilitySeverity.Critical);
            scan.HighCount = vulns.Count(v => v.Severity == VulnerabilitySeverity.High);
            scan.MediumCount = vulns.Count(v => v.Severity == VulnerabilitySeverity.Medium);
            scan.LowCount = vulns.Count(v => v.Severity == VulnerabilitySeverity.Low);
        }

        await db.SaveChangesAsync();
        return scan;
    }

    public static IReadOnlyList<(string, VulnerabilitySeverity)> Findings(string prefix, int critical = 0, int high = 0, int medium = 0, int low = 0)
    {
        var list = new List<(string, VulnerabilitySeverity)>();
        void Add(int n, VulnerabilitySeverity sev, string tag) { for (var i = 0; i < n; i++) list.Add(($"{prefix}-{tag}{i}", sev)); }
        Add(critical, VulnerabilitySeverity.Critical, "C");
        Add(high, VulnerabilitySeverity.High, "H");
        Add(medium, VulnerabilitySeverity.Medium, "M");
        Add(low, VulnerabilitySeverity.Low, "L");
        return list;
    }
}
