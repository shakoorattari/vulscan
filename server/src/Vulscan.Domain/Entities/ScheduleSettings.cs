using Vulscan.Domain.Common;

namespace Vulscan.Domain.Entities;

/// <summary>
/// Singleton entity that holds the global default cron schedule applied to every
/// enabled <see cref="Project"/> that does not have its own <see cref="Project.CronExpression"/>.
/// Identified by a fixed Guid so there is exactly one row.
/// </summary>
public class ScheduleSettings : BaseEntity
{
    /// <summary>Fixed Id for the singleton row.</summary>
    public static readonly Guid SingletonId = new("11111111-1111-1111-1111-111111111111");

    /// <summary>Standard 5-field cron (minute hour day month weekday) interpreted in UTC. Default = daily 02:00.</summary>
    public string CronExpression { get; set; } = "0 2 * * *";

    /// <summary>Master switch — when false the scheduler does not enqueue any project.</summary>
    public bool Enabled { get; set; } = true;
}
