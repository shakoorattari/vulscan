using Vulscan.Application.DTOs.Email;

namespace Vulscan.Application.Interfaces;

/// <summary>
/// Service for sending email notifications.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send a test email to verify SMTP configuration.
    /// </summary>
    Task<(bool Success, string Message)> SendTestEmailAsync(string toEmail, string? subject = null, string? body = null, CancellationToken ct = default);

    /// <summary>
    /// Send scan completion notification with vulnerability report.
    /// </summary>
    Task<(bool Success, string Message)> SendScanNotificationAsync(
        Guid scanRunId,
        bool includePdfAttachment = true,
        bool includeHtmlAttachment = false,
        string[]? additionalRecipients = null,
        CancellationToken ct = default);

    /// <summary>
    /// Generate HTML email body for scan notification.
    /// </summary>
    Task<string> GenerateScanEmailHtmlAsync(Guid scanRunId, CancellationToken ct = default);

    /// <summary>
    /// Generate PDF attachment for scan report.
    /// </summary>
    Task<byte[]> GenerateScanReportPdfAsync(Guid scanRunId, CancellationToken ct = default);

    /// <summary>
    /// Generate HTML attachment for scan report.
    /// </summary>
    Task<string> GenerateScanReportHtmlAsync(Guid scanRunId, CancellationToken ct = default);

    /// <summary>
    /// Get active SMTP configuration.
    /// </summary>
    Task<SmtpConfigurationDto?> GetActiveSmtpConfigurationAsync(CancellationToken ct = default);

    /// <summary>
    /// Check if email notifications are configured and enabled.
    /// </summary>
    Task<bool> IsEmailEnabledAsync(CancellationToken ct = default);
}
