using Vulscan.Domain.Common;

namespace Vulscan.Domain.Entities;

/// <summary>
/// SMTP server configuration for sending email notifications.
/// Only one active configuration should exist at a time.
/// </summary>
public class SmtpConfiguration : BaseEntity
{
    /// <summary>SMTP server hostname or IP address.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>SMTP server port (typically 25, 587, or 465).</summary>
    public int Port { get; set; } = 587;

    /// <summary>Whether to use SSL/TLS encryption.</summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>Whether to use STARTTLS.</summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>SMTP username for authentication (optional).</summary>
    public string? Username { get; set; }

    /// <summary>SMTP password for authentication (optional, encrypted in DB).</summary>
    public string? Password { get; set; }

    /// <summary>"From" email address for outgoing emails.</summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>"From" display name for outgoing emails.</summary>
    public string FromName { get; set; } = "Vulscan Security Platform";

    /// <summary>
    /// Reply-to email address (optional). If not set, uses FromEmail.
    /// </summary>
    public string? ReplyToEmail { get; set; }

    /// <summary>SMTP connection timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Whether this configuration is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Whether email notifications are globally enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Last time a test email was successfully sent.</summary>
    public DateTime? LastTestedAt { get; set; }

    /// <summary>Result of last test email attempt.</summary>
    public string? LastTestResult { get; set; }
}
