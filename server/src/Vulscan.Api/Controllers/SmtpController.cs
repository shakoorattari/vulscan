using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Email;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Admin")]
public class SmtpController : ControllerBase
{
    private readonly ISmtpConfigurationService _smtpConfigService;
    private readonly IEmailService _emailService;
    private readonly ILogger<SmtpController> _logger;

    public SmtpController(
        ISmtpConfigurationService smtpConfigService,
        IEmailService emailService,
        ILogger<SmtpController> logger)
    {
        _smtpConfigService = smtpConfigService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Get active SMTP configuration.
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<ApiResponse<SmtpConfigurationDto>>> GetActiveConfiguration(CancellationToken ct)
    {
        var config = await _smtpConfigService.GetActiveConfigurationAsync(ct);
        if (config == null)
            return NotFound(ApiResponse<SmtpConfigurationDto>.Fail("No active SMTP configuration found"));

        return Ok(ApiResponse<SmtpConfigurationDto>.Ok(config));
    }

    /// <summary>
    /// Get all SMTP configurations.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<SmtpConfigurationDto>>>> GetAll(CancellationToken ct)
    {
        var configs = await _smtpConfigService.GetAllAsync(ct);
        return Ok(ApiResponse<IEnumerable<SmtpConfigurationDto>>.Ok(configs));
    }

    /// <summary>
    /// Get SMTP configuration by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SmtpConfigurationDto>>> GetById(Guid id, CancellationToken ct)
    {
        var config = await _smtpConfigService.GetByIdAsync(id, ct);
        if (config == null)
            return NotFound(ApiResponse<SmtpConfigurationDto>.Fail("SMTP configuration not found"));

        return Ok(ApiResponse<SmtpConfigurationDto>.Ok(config));
    }

    /// <summary>
    /// Create new SMTP configuration.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<SmtpConfigurationDto>>> Create(
        [FromBody] SmtpConfigurationRequest request,
        CancellationToken ct)
    {
        try
        {
            var config = await _smtpConfigService.CreateAsync(request, ct);
            return CreatedAtAction(
                nameof(GetById),
                new { id = config.Id },
                ApiResponse<SmtpConfigurationDto>.Ok(config, "SMTP configuration created successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create SMTP configuration");
            return BadRequest(ApiResponse<SmtpConfigurationDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Update SMTP configuration.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SmtpConfigurationDto>>> Update(
        Guid id,
        [FromBody] SmtpConfigurationRequest request,
        CancellationToken ct)
    {
        try
        {
            var config = await _smtpConfigService.UpdateAsync(id, request, ct);
            return Ok(ApiResponse<SmtpConfigurationDto>.Ok(config, "SMTP configuration updated successfully"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<SmtpConfigurationDto>.Fail("SMTP configuration not found"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update SMTP configuration {ConfigId}", id);
            return BadRequest(ApiResponse<SmtpConfigurationDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Delete SMTP configuration.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _smtpConfigService.DeleteAsync(id, ct);
            return Ok(ApiResponse.Ok("SMTP configuration deleted successfully"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse.Fail("SMTP configuration not found"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete SMTP configuration {ConfigId}", id);
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Set a configuration as active.
    /// </summary>
    [HttpPost("{id:guid}/set-active")]
    public async Task<ActionResult<ApiResponse<SmtpConfigurationDto>>> SetActive(Guid id, CancellationToken ct)
    {
        try
        {
            var config = await _smtpConfigService.SetActiveAsync(id, ct);
            return Ok(ApiResponse<SmtpConfigurationDto>.Ok(config, "SMTP configuration activated successfully"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<SmtpConfigurationDto>.Fail("SMTP configuration not found"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate SMTP configuration {ConfigId}", id);
            return BadRequest(ApiResponse<SmtpConfigurationDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Test SMTP configuration by sending a test email.
    /// </summary>
    [HttpPost("{id:guid}/test")]
    public async Task<ActionResult<ApiResponse<object>>> TestConfiguration(
        Guid id,
        [FromBody] TestEmailRequest request,
        CancellationToken ct)
    {
        try
        {
            var (success, message) = await _smtpConfigService.TestConfigurationAsync(id, request.ToEmail, ct);
            
            if (success)
            {
                // Actually send a test email
                var (emailSuccess, emailMessage) = await _emailService.SendTestEmailAsync(
                    request.ToEmail,
                    request.Subject,
                    request.Body,
                    ct);

                return Ok(ApiResponse<object>.Ok(new { success = emailSuccess, message = emailMessage }));
            }

            return BadRequest(ApiResponse<object>.Fail(message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test SMTP configuration {ConfigId}", id);
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Send scan notification email manually.
    /// </summary>
    [HttpPost("send-scan-notification")]
    [Authorize(Roles = "Admin,SecurityAnalyst")]
    public async Task<ActionResult<ApiResponse<object>>> SendScanNotification(
        [FromBody] SendScanNotificationRequest request,
        CancellationToken ct)
    {
        try
        {
            var (success, message) = await _emailService.SendScanNotificationAsync(
                request.ScanRunId,
                request.IncludePdfAttachment,
                request.IncludeHtmlAttachment,
                request.AdditionalRecipients,
                ct);

            if (success)
                return Ok(ApiResponse<object>.Ok(new { message }));

            return BadRequest(ApiResponse<object>.Fail(message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send scan notification for {ScanRunId}", request.ScanRunId);
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Check if email notifications are enabled.
    /// </summary>
    [HttpGet("status")]
    [AllowAnonymous] // Allow all authenticated users to check status
    public async Task<ActionResult<ApiResponse<object>>> GetEmailStatus(CancellationToken ct)
    {
        var isEnabled = await _emailService.IsEmailEnabledAsync(ct);
        var config = await _emailService.GetActiveSmtpConfigurationAsync(ct);

        return Ok(ApiResponse<object>.Ok(new
        {
            enabled = isEnabled,
            configured = config != null,
            fromEmail = config?.FromEmail,
            fromName = config?.FromName
        }));
    }
}
