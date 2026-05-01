using Vulscan.Domain.Common;
using Vulscan.Domain.Enums;

namespace Vulscan.Domain.Entities;

/// <summary>
/// Log record of sent email notifications.
/// </summary>
public class EmailLog : BaseEntity
{
    /// <summary>Recipient email address(es) (comma-separated).</summary>
    public string ToEmails { get; set; } = string.Empty;

    /// <summary>CC email address(es) (comma-separated, optional).</summary>
    public string? CcEmails { get; set; }

    /// <summary>Email subject line.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Email body (HTML or plain text).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Whether email was sent successfully.</summary>
    public bool IsSent { get; set; }

    /// <summary>Error message if sending failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Time when email was sent (or attempted).</summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>Scan run that triggered this email (optional).</summary>
    public Guid? ScanRunId { get; set; }

    /// <summary>Project associated with this email (optional).</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>User who triggered the email (optional).</summary>
    public Guid? TriggeredByUserId { get; set; }

    /// <summary>Type of email (e.g., ScanComplete, TestEmail, Alert).</summary>
    public string EmailType { get; set; } = "ScanComplete";

    /// <summary>Size of attachments in bytes.</summary>
    public long? AttachmentSize { get; set; }

    /// <summary>Number of retries attempted.</summary>
    public int RetryCount { get; set; } = 0;

    // Navigation
    public ScanRun? ScanRun { get; set; }
    public Project? Project { get; set; }
    public User? TriggeredByUser { get; set; }
}
