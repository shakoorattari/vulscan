using System.ComponentModel.DataAnnotations;

namespace Vulscan.Application.DTOs.Settings;

public sealed record ScheduleSettingsDto
{
    public required string CronExpression { get; init; }
    public required string CronDescription { get; init; }
    public required bool Enabled { get; init; }
    public DateTime? NextRunUtc { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed record UpdateScheduleSettingsRequest
{
    /// <summary>5-field cron (minute hour dom month dow), interpreted in UTC.</summary>
    [Required]
    [StringLength(100, MinimumLength = 5)]
    public string CronExpression { get; init; } = "0 2 * * *";

    public bool Enabled { get; init; } = true;
}
