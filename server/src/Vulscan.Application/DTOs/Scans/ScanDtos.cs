using System.ComponentModel.DataAnnotations;

namespace Vulscan.Application.DTOs.Scans;

public sealed record ScanRunDto
{
    public Guid Id { get; init; }
    public Guid? InstanceId { get; init; }
    public string? InstanceName { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int DurationSeconds { get; init; }
    public string Status { get; init; } = string.Empty;
    public int ReposScanned { get; init; }
    public int ReposFailed { get; init; }
    public int TotalVulnerabilities { get; init; }
    public int CriticalCount { get; init; }
    public int HighCount { get; init; }
    public int MediumCount { get; init; }
    public int LowCount { get; init; }
    public string? TriggeredBy { get; init; }
    public string? ErrorLog { get; init; }
}

public sealed record TriggerScanRequest
{
    [Required]
    public Guid InstanceId { get; init; }
}

public sealed record TriggerScanResponse
{
    public Guid ScanRunId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
