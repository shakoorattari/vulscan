namespace Vulscan.Application.DTOs.Dashboard;

public sealed record DashboardSummaryDto
{
    public int TotalRepositories { get; init; }
    public int TotalScans { get; init; }
    public int TotalVulnerabilities { get; init; }
    public int CriticalCount { get; init; }
    public int HighCount { get; init; }
    public int MediumCount { get; init; }
    public int LowCount { get; init; }
    public int NegligibleCount { get; init; }
    public DateTime? LastScanDate { get; init; }
    public string? LastScanStatus { get; init; }
    public List<RecentScanDto> RecentScans { get; init; } = [];
    public List<TopVulnerableRepoDto> TopVulnerableRepos { get; init; } = [];
}

public sealed record RecentScanDto
{
    public Guid Id { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public int ReposScanned { get; init; }
    public int TotalVulnerabilities { get; init; }
    public string? TriggeredBy { get; init; }
}

public sealed record TopVulnerableRepoDto
{
    public Guid RepositoryId { get; init; }
    public string RepositoryName { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public int CriticalCount { get; init; }
    public int HighCount { get; init; }
    public int TotalVulnerabilities { get; init; }
}
