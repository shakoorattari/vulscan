using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vulscan.Application.DTOs.Email;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Infrastructure.Data;

namespace Vulscan.Infrastructure.Services;

public class SmtpConfigurationService : ISmtpConfigurationService
{
    private readonly VulscanDbContext _dbContext;
    private readonly ILogger<SmtpConfigurationService> _logger;

    public SmtpConfigurationService(
        VulscanDbContext dbContext,
        ILogger<SmtpConfigurationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<SmtpConfigurationDto?> GetActiveConfigurationAsync(CancellationToken ct = default)
    {
        var config = await _dbContext.SmtpConfigurations
            .Where(c => c.IsActive && c.IsEnabled)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return config == null ? null : MapToDto(config);
    }

    public async Task<SmtpConfigurationDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var config = await _dbContext.SmtpConfigurations.FindAsync([id], ct);
        return config == null ? null : MapToDto(config);
    }

    public async Task<IEnumerable<SmtpConfigurationDto>> GetAllAsync(CancellationToken ct = default)
    {
        var configs = await _dbContext.SmtpConfigurations
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return configs.Select(MapToDto);
    }

    public async Task<SmtpConfigurationDto> CreateAsync(SmtpConfigurationRequest request, CancellationToken ct = default)
    {
        var config = new SmtpConfiguration
        {
            Host = request.Host,
            Port = request.Port,
            UseSsl = request.UseSsl,
            UseStartTls = request.UseStartTls,
            Username = request.Username,
            Password = request.Password, // TODO: Encrypt in production
            FromEmail = request.FromEmail,
            FromName = request.FromName,
            ReplyToEmail = request.ReplyToEmail,
            TimeoutSeconds = request.TimeoutSeconds,
            IsEnabled = request.IsEnabled,
            IsActive = false // Don't auto-activate, admin must explicitly set active
        };

        _dbContext.SmtpConfigurations.Add(config);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created SMTP configuration {ConfigId} for {Host}:{Port}", 
            config.Id, config.Host, config.Port);

        return MapToDto(config);
    }

    public async Task<SmtpConfigurationDto> UpdateAsync(Guid id, SmtpConfigurationRequest request, CancellationToken ct = default)
    {
        var config = await _dbContext.SmtpConfigurations.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"SMTP configuration {id} not found");

        config.Host = request.Host;
        config.Port = request.Port;
        config.UseSsl = request.UseSsl;
        config.UseStartTls = request.UseStartTls;
        config.Username = request.Username;
        
        // Only update password if provided
        if (!string.IsNullOrEmpty(request.Password))
            config.Password = request.Password; // TODO: Encrypt in production

        config.FromEmail = request.FromEmail;
        config.FromName = request.FromName;
        config.ReplyToEmail = request.ReplyToEmail;
        config.TimeoutSeconds = request.TimeoutSeconds;
        config.IsEnabled = request.IsEnabled;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Updated SMTP configuration {ConfigId}", config.Id);

        return MapToDto(config);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var config = await _dbContext.SmtpConfigurations.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"SMTP configuration {id} not found");

        if (config.IsActive)
            throw new InvalidOperationException("Cannot delete active SMTP configuration. Deactivate it first.");

        _dbContext.SmtpConfigurations.Remove(config);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted SMTP configuration {ConfigId}", id);
    }

    public async Task<SmtpConfigurationDto> SetActiveAsync(Guid id, CancellationToken ct = default)
    {
        // Deactivate all existing configurations
        var allConfigs = await _dbContext.SmtpConfigurations.ToListAsync(ct);
        foreach (var c in allConfigs)
            c.IsActive = false;

        // Activate the specified one
        var config = allConfigs.FirstOrDefault(c => c.Id == id)
            ?? throw new KeyNotFoundException($"SMTP configuration {id} not found");

        config.IsActive = true;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Set SMTP configuration {ConfigId} as active", id);

        return MapToDto(config);
    }

    public async Task<(bool Success, string Message)> TestConfigurationAsync(Guid id, string testEmail, CancellationToken ct = default)
    {
        var config = await _dbContext.SmtpConfigurations.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"SMTP configuration {id} not found");

        try
        {
            // We'll use the email service to send the test
            // For now, just validate the configuration
            if (string.IsNullOrWhiteSpace(config.Host))
                return (false, "SMTP host is required");

            if (config.Port <= 0 || config.Port > 65535)
                return (false, "Invalid port number");

            if (string.IsNullOrWhiteSpace(config.FromEmail))
                return (false, "From email address is required");

            config.LastTestedAt = DateTime.UtcNow;
            config.LastTestResult = "Validation passed (actual email not sent in test mode)";
            await _dbContext.SaveChangesAsync(ct);

            return (true, "SMTP configuration validated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test SMTP configuration {ConfigId}", id);
            config.LastTestResult = $"Error: {ex.Message}";
            await _dbContext.SaveChangesAsync(ct);
            return (false, ex.Message);
        }
    }

    private static SmtpConfigurationDto MapToDto(SmtpConfiguration config) => new()
    {
        Id = config.Id,
        Host = config.Host,
        Port = config.Port,
        UseSsl = config.UseSsl,
        UseStartTls = config.UseStartTls,
        Username = config.Username,
        // Don't expose password in DTO
        FromEmail = config.FromEmail,
        FromName = config.FromName,
        ReplyToEmail = config.ReplyToEmail,
        TimeoutSeconds = config.TimeoutSeconds,
        IsActive = config.IsActive,
        IsEnabled = config.IsEnabled,
        LastTestedAt = config.LastTestedAt,
        LastTestResult = config.LastTestResult,
        CreatedAt = config.CreatedAt,
        UpdatedAt = config.UpdatedAt
    };
}
