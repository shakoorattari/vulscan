using Vulscan.Domain.Common;

namespace Vulscan.Domain.Entities;

/// <summary>
/// Audit trail for user actions within the system.
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>Id of the acting user, when known. Not a foreign key (see class remarks).</summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Username as presented at the time of the event. Kept separately from <see cref="UserId"/> so
    /// failed sign-ins for unknown accounts, and events for later-deleted users, remain attributable.
    /// </summary>
    public string? Username { get; set; }

    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }

    /// <summary>Success, Failure or Denied.</summary>
    public string Outcome { get; set; } = "Success";

    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Deliberately no navigation to User and no foreign key: the audit trail must outlive, and never depend on,
    // the users it describes. Attribution is preserved in UserId + Username.
}
