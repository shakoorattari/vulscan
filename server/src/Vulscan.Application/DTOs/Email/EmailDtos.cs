namespace Vulscan.Application.DTOs.Email;

/// <summary>
/// SMTP configuration DTO for API responses.
/// </summary>
public record SmtpConfigurationDto
{
    public Guid Id { get; init; }
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public bool UseSsl { get; init; }
    public bool UseStartTls { get; init; }
    public string? Username { get; init; }
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
    public string? ReplyToEmail { get; init; }
    public int TimeoutSeconds { get; init; }
    public bool IsActive { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime? LastTestedAt { get; init; }
    public string? LastTestResult { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Request DTO for creating or updating SMTP configuration.
/// </summary>
public record SmtpConfigurationRequest
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public bool UseStartTls { get; init; } = true;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = "Vulscan Security Platform";
    public string? ReplyToEmail { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public bool IsEnabled { get; init; } = true;
}

/// <summary>
/// Request to test SMTP configuration.
/// </summary>
public record TestEmailRequest
{
    public string ToEmail { get; init; } = string.Empty;
    public string? Subject { get; init; }
    public string? Body { get; init; }
}

/// <summary>
/// Email log DTO for API responses.
/// </summary>
public record EmailLogDto
{
    public Guid Id { get; init; }
    public string ToEmails { get; init; } = string.Empty;
    public string? CcEmails { get; init; }
    public string Subject { get; init; } = string.Empty;
    public bool IsSent { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime SentAt { get; init; }
    public Guid? ScanRunId { get; init; }
    public Guid? ProjectId { get; init; }
    public string EmailType { get; init; } = string.Empty;
    public long? AttachmentSize { get; init; }
    public int RetryCount { get; init; }
}

/// <summary>
/// Request to send scan notification email.
/// </summary>
public record SendScanNotificationRequest
{
    public Guid ScanRunId { get; init; }
    public bool IncludePdfAttachment { get; init; } = true;
    public bool IncludeHtmlAttachment { get; init; } = false;
    public string[]? AdditionalRecipients { get; init; }
}
