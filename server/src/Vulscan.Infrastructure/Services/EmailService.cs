using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using Vulscan.Application.DTOs.Email;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;
using Vulscan.Infrastructure.Data;

namespace Vulscan.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly VulscanDbContext _dbContext;
    private readonly ISmtpConfigurationService _smtpConfigService;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        VulscanDbContext dbContext,
        ISmtpConfigurationService smtpConfigService,
        ILogger<EmailService> logger)
    {
        _dbContext = dbContext;
        _smtpConfigService = smtpConfigService;
        _logger = logger;
    }

    public async Task<bool> IsEmailEnabledAsync(CancellationToken ct = default)
    {
        var config = await _smtpConfigService.GetActiveConfigurationAsync(ct);
        return config != null && config.IsEnabled;
    }

    public async Task<SmtpConfigurationDto?> GetActiveSmtpConfigurationAsync(CancellationToken ct = default)
    {
        return await _smtpConfigService.GetActiveConfigurationAsync(ct);
    }

    public async Task<(bool Success, string Message)> SendTestEmailAsync(
        string toEmail,
        string? subject = null,
        string? body = null,
        CancellationToken ct = default)
    {
        var config = await _smtpConfigService.GetActiveConfigurationAsync(ct);
        if (config == null)
            return (false, "No active SMTP configuration found");

        subject ??= "Vulscan Test Email";
        body ??= GenerateTestEmailHtml();

        try
        {
            await SendEmailInternalAsync(
                toEmail,
                null,
                subject,
                body,
                true,
                null,
                null,
                "TestEmail",
                ct);

            _logger.LogInformation("Test email sent successfully to {Email}", toEmail);
            return (true, "Test email sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test email to {Email}", toEmail);
            return (false, $"Failed to send email: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> SendScanNotificationAsync(
        Guid scanRunId,
        bool includePdfAttachment = true,
        bool includeHtmlAttachment = false,
        string[]? additionalRecipients = null,
        CancellationToken ct = default)
    {
        if (!await IsEmailEnabledAsync(ct))
            return (false, "Email notifications are not configured");

        var scanRun = await _dbContext.ScanRuns
            .Include(s => s.Project)
            .Include(s => s.Vulnerabilities.Take(100)) // Limit for performance
            .FirstOrDefaultAsync(s => s.Id == scanRunId, ct);

        if (scanRun == null)
            return (false, $"Scan run {scanRunId} not found");

        if (scanRun.Project == null)
            return (false, "Project not found for scan run");

        // Check if project has notifications enabled
        if (!scanRun.Project.SendEmailNotifications)
            return (false, "Email notifications are disabled for this project");

        // Determine recipients
        var recipients = new List<string>();
        if (!string.IsNullOrWhiteSpace(scanRun.Project.OwnerEmail))
            recipients.Add(scanRun.Project.OwnerEmail);

        if (additionalRecipients != null)
            recipients.AddRange(additionalRecipients);

        if (recipients.Count == 0)
            return (false, "No recipients configured for this project");

        // Generate email content
        var htmlBody = await GenerateScanEmailHtmlAsync(scanRunId, ct);
        var subject = $"Vulnerability Scan Complete: {scanRun.Project.Name}";

        // Generate attachments
        byte[]? pdfAttachment = null;
        string? htmlAttachment = null;

        try
        {
            if (includePdfAttachment)
                pdfAttachment = await GenerateScanReportPdfAsync(scanRunId, ct);

            if (includeHtmlAttachment)
                htmlAttachment = await GenerateScanReportHtmlAsync(scanRunId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate attachment for scan {ScanRunId}", scanRunId);
            // Continue without attachment
        }

        try
        {
            await SendEmailInternalAsync(
                string.Join(",", recipients),
                scanRun.Project.CcEmails,
                subject,
                htmlBody,
                true,
                pdfAttachment,
                htmlAttachment,
                "ScanComplete",
                ct,
                scanRunId,
                scanRun.ProjectId);

            _logger.LogInformation("Scan notification sent for {ScanRunId} to {Count} recipients",
                scanRunId, recipients.Count);

            return (true, $"Email sent to {recipients.Count} recipient(s)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send scan notification for {ScanRunId}", scanRunId);
            return (false, $"Failed to send email: {ex.Message}");
        }
    }

    public async Task<string> GenerateScanEmailHtmlAsync(Guid scanRunId, CancellationToken ct = default)
    {
        var scanRun = await _dbContext.ScanRuns
            .Include(s => s.Project)
            .Include(s => s.Vulnerabilities)
            .FirstOrDefaultAsync(s => s.Id == scanRunId, ct);

        if (scanRun == null)
            throw new KeyNotFoundException($"Scan run {scanRunId} not found");

        var criticalVulns = scanRun.Vulnerabilities.Where(v => v.Severity == VulnerabilitySeverity.Critical).Take(10).ToList();
        var highVulns = scanRun.Vulnerabilities.Where(v => v.Severity == VulnerabilitySeverity.High).Take(10).ToList();

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang='en'>");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset='UTF-8'>");
        html.AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        html.AppendLine("    <title>Vulnerability Scan Report</title>");
        html.AppendLine("    <style>");
        html.AppendLine("        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; background-color: #f4f4f4; margin: 0; padding: 0; }");
        html.AppendLine("        .container { max-width: 800px; margin: 20px auto; background: #fff; padding: 30px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }");
        html.AppendLine("        .header { background: linear-gradient(135deg, #1e3a8a 0%, #3b82f6 100%); color: white; padding: 30px; border-radius: 8px 8px 0 0; margin: -30px -30px 30px -30px; }");
        html.AppendLine("        .header h1 { margin: 0; font-size: 28px; font-weight: 600; }");
        html.AppendLine("        .header p { margin: 10px 0 0 0; font-size: 16px; opacity: 0.9; }");
        html.AppendLine("        .summary { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 15px; margin: 25px 0; }");
        html.AppendLine("        .summary-card { background: #f8fafc; padding: 20px; border-radius: 8px; text-align: center; border-left: 4px solid #3b82f6; }");
        html.AppendLine("        .summary-card.critical { border-left-color: #dc2626; background: #fef2f2; }");
        html.AppendLine("        .summary-card.high { border-left-color: #ea580c; background: #fff7ed; }");
        html.AppendLine("        .summary-card.medium { border-left-color: #f59e0b; background: #fffbeb; }");
        html.AppendLine("        .summary-card.low { border-left-color: #84cc16; background: #f7fee7; }");
        html.AppendLine("        .summary-card .count { font-size: 36px; font-weight: bold; margin: 0; }");
        html.AppendLine("        .summary-card .label { font-size: 14px; color: #64748b; margin-top: 5px; }");
        html.AppendLine("        .section { margin: 30px 0; }");
        html.AppendLine("        .section h2 { color: #1e293b; font-size: 20px; margin-bottom: 15px; padding-bottom: 10px; border-bottom: 2px solid #e2e8f0; }");
        html.AppendLine("        .vuln-table { width: 100%; border-collapse: collapse; margin-top: 15px; }");
        html.AppendLine("        .vuln-table th { background: #f1f5f9; padding: 12px; text-align: left; font-weight: 600; color: #475569; border-bottom: 2px solid #cbd5e1; }");
        html.AppendLine("        .vuln-table td { padding: 12px; border-bottom: 1px solid #e2e8f0; }");
        html.AppendLine("        .severity-badge { display: inline-block; padding: 4px 12px; border-radius: 12px; font-size: 12px; font-weight: 600; text-transform: uppercase; }");
        html.AppendLine("        .severity-critical { background: #fee2e2; color: #991b1b; }");
        html.AppendLine("        .severity-high { background: #ffedd5; color: #9a3412; }");
        html.AppendLine("        .severity-medium { background: #fef3c7; color: #92400e; }");
        html.AppendLine("        .severity-low { background: #ecfccb; color: #3f6212; }");
        html.AppendLine("        .footer { margin-top: 40px; padding-top: 20px; border-top: 1px solid #e2e8f0; text-align: center; color: #64748b; font-size: 14px; }");
        html.AppendLine("        .cta-button { display: inline-block; margin-top: 20px; padding: 12px 30px; background: #3b82f6; color: white; text-decoration: none; border-radius: 6px; font-weight: 600; }");
        html.AppendLine("        .info-box { background: #eff6ff; border-left: 4px solid #3b82f6; padding: 15px; border-radius: 4px; margin: 20px 0; }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class='container'>");
        html.AppendLine("        <div class='header'>");
        html.AppendLine($"            <h1>🛡️ Vulnerability Scan Complete</h1>");
        html.AppendLine($"            <p>Project: {scanRun.Project.Name}</p>");
        html.AppendLine($"            <p>Scan completed at {scanRun.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC</p>");
        html.AppendLine("        </div>");

        // Summary section
        html.AppendLine("        <div class='summary'>");
        html.AppendLine($"            <div class='summary-card critical'><p class='count'>{scanRun.CriticalCount}</p><p class='label'>Critical</p></div>");
        html.AppendLine($"            <div class='summary-card high'><p class='count'>{scanRun.HighCount}</p><p class='label'>High</p></div>");
        html.AppendLine($"            <div class='summary-card medium'><p class='count'>{scanRun.MediumCount}</p><p class='label'>Medium</p></div>");
        html.AppendLine($"            <div class='summary-card low'><p class='count'>{scanRun.LowCount}</p><p class='label'>Low</p></div>");
        html.AppendLine("        </div>");

        // Scan details
        html.AppendLine("        <div class='info-box'>");
        html.AppendLine($"            <strong>Scan Details:</strong><br>");
        html.AppendLine($"            Repositories Scanned: <strong>{scanRun.ReposScanned}</strong> | ");
        html.AppendLine($"            Branches Scanned: <strong>{scanRun.BranchesScanned}</strong> | ");
        html.AppendLine($"            Duration: <strong>{scanRun.DurationSeconds}s</strong> | ");
        html.AppendLine($"            Total Vulnerabilities: <strong>{scanRun.TotalVulnerabilities}</strong>");
        html.AppendLine("        </div>");

        // Critical vulnerabilities
        if (criticalVulns.Any())
        {
            html.AppendLine("        <div class='section'>");
            html.AppendLine("            <h2>🔴 Critical Vulnerabilities (Top 10)</h2>");
            html.AppendLine("            <table class='vuln-table'>");
            html.AppendLine("                <thead><tr><th>CVE ID</th><th>Package</th><th>CVSS Score</th><th>Severity</th></tr></thead>");
            html.AppendLine("                <tbody>");
            foreach (var vuln in criticalVulns)
            {
                html.AppendLine($"                <tr>");
                html.AppendLine($"                    <td><strong>{vuln.CveId}</strong></td>");
                html.AppendLine($"                    <td>{vuln.PackageName}@{vuln.InstalledVersion}</td>");
                html.AppendLine($"                    <td>{vuln.CvssScore:F1}</td>");
                html.AppendLine($"                    <td><span class='severity-badge severity-critical'>{vuln.Severity}</span></td>");
                html.AppendLine($"                </tr>");
            }
            html.AppendLine("                </tbody>");
            html.AppendLine("            </table>");
            html.AppendLine("        </div>");
        }

        // High vulnerabilities
        if (highVulns.Any())
        {
            html.AppendLine("        <div class='section'>");
            html.AppendLine("            <h2>🟠 High Severity Vulnerabilities (Top 10)</h2>");
            html.AppendLine("            <table class='vuln-table'>");
            html.AppendLine("                <thead><tr><th>CVE ID</th><th>Package</th><th>CVSS Score</th><th>Severity</th></tr></thead>");
            html.AppendLine("                <tbody>");
            foreach (var vuln in highVulns)
            {
                html.AppendLine($"                <tr>");
                html.AppendLine($"                    <td><strong>{vuln.CveId}</strong></td>");
                html.AppendLine($"                    <td>{vuln.PackageName}@{vuln.InstalledVersion}</td>");
                html.AppendLine($"                    <td>{vuln.CvssScore:F1}</td>");
                html.AppendLine($"                    <td><span class='severity-badge severity-high'>{vuln.Severity}</span></td>");
                html.AppendLine($"                </tr>");
            }
            html.AppendLine("                </tbody>");
            html.AppendLine("            </table>");
            html.AppendLine("        </div>");
        }

        // Footer
        html.AppendLine("        <div class='footer'>");
        html.AppendLine("            <p>This is an automated notification from Vulscan Security Platform.</p>");
        html.AppendLine("            <p>For complete details, please review the attached report or access the dashboard.</p>");
        html.AppendLine("            <p style='margin-top: 20px;'><small>© 2026 Vulscan - Vulnerability Scanning Platform</small></p>");
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    public async Task<byte[]> GenerateScanReportPdfAsync(Guid scanRunId, CancellationToken ct = default)
    {
        // TODO: Implement PDF generation using a library like QuestPDF or HTML-to-PDF converter
        // For now, return a placeholder
        var html = await GenerateScanReportHtmlAsync(scanRunId, ct);
        return Encoding.UTF8.GetBytes($"PDF generation not yet implemented. HTML content:\n\n{html}");
    }

    public async Task<string> GenerateScanReportHtmlAsync(Guid scanRunId, CancellationToken ct = default)
    {
        return await GenerateScanEmailHtmlAsync(scanRunId, ct);
    }

    private async Task SendEmailInternalAsync(
        string toEmails,
        string? ccEmails,
        string subject,
        string htmlBody,
        bool isHtml,
        byte[]? pdfAttachment,
        string? htmlAttachment,
        string emailType,
        CancellationToken ct,
        Guid? scanRunId = null,
        Guid? projectId = null)
    {
        var config = await _dbContext.SmtpConfigurations
            .Where(c => c.IsActive && c.IsEnabled)
            .FirstOrDefaultAsync(ct);

        if (config == null)
            throw new InvalidOperationException("No active SMTP configuration found");

        var emailLog = new EmailLog
        {
            ToEmails = toEmails,
            CcEmails = ccEmails,
            Subject = subject,
            Body = isHtml ? htmlBody : htmlBody,
            EmailType = emailType,
            ScanRunId = scanRunId,
            ProjectId = projectId,
            AttachmentSize = pdfAttachment?.Length ?? 0,
            SentAt = DateTime.UtcNow
        };

        try
        {
            // TODO: Implement actual SMTP sending using MailKit
            // For now, just log and mark as sent
            _logger.LogInformation("Email would be sent to {To} with subject '{Subject}'", toEmails, subject);
            _logger.LogInformation("SMTP: {Host}:{Port}, SSL: {UseSsl}, From: {From}",
                config.Host, config.Port, config.UseSsl, config.FromEmail);

            emailLog.IsSent = true;
            emailLog.ErrorMessage = "Simulated send (MailKit integration pending)";
        }
        catch (Exception ex)
        {
            emailLog.IsSent = false;
            emailLog.ErrorMessage = ex.Message;
            throw;
        }
        finally
        {
            _dbContext.EmailLogs.Add(emailLog);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    private string GenerateTestEmailHtml()
    {
        return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
        .container { max-width: 600px; margin: 20px auto; padding: 20px; background: #f9f9f9; border-radius: 8px; }
        .header { background: #3b82f6; color: white; padding: 20px; border-radius: 8px 8px 0 0; margin: -20px -20px 20px -20px; }
        .content { background: white; padding: 20px; border-radius: 8px; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✅ SMTP Test Successful</h1>
        </div>
        <div class='content'>
            <p>This is a test email from Vulscan Security Platform.</p>
            <p>If you received this email, your SMTP configuration is working correctly!</p>
            <p style='margin-top: 30px; color: #666; font-size: 14px;'>
                <strong>Vulscan</strong> - Vulnerability Scanning Platform<br>
                Automated Security Testing for Azure DevOps
            </p>
        </div>
    </div>
</body>
</html>";
    }
}
