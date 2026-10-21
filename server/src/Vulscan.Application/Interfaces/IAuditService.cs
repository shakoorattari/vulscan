namespace Vulscan.Application.Interfaces;

public static class AuditOutcome
{
    public const string Success = "Success";
    public const string Failure = "Failure";
    public const string Denied = "Denied";
}

/// <summary>
/// An event to record in the audit trail. User, username and IP default to the current request context
/// when not supplied; pass them explicitly for events that happen before a user is authenticated.
/// </summary>
public sealed record AuditEntry(
    string Action,
    string EntityType,
    string? EntityId = null,
    string? Details = null,
    string Outcome = AuditOutcome.Success,
    Guid? UserId = null,
    string? Username = null,
    string? IpAddress = null);

public interface IAuditService
{
    /// <summary>
    /// Writes an audit record. Never throws: a failure to audit is logged but must not break the audited operation.
    /// </summary>
    Task LogAsync(AuditEntry entry, CancellationToken ct = default);
}
