using Vulscan.Application.DTOs.Email;

namespace Vulscan.Application.Interfaces;

/// <summary>
/// Service for managing SMTP configuration.
/// </summary>
public interface ISmtpConfigurationService
{
    Task<SmtpConfigurationDto?> GetActiveConfigurationAsync(CancellationToken ct = default);
    Task<SmtpConfigurationDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<SmtpConfigurationDto>> GetAllAsync(CancellationToken ct = default);
    Task<SmtpConfigurationDto> CreateAsync(SmtpConfigurationRequest request, CancellationToken ct = default);
    Task<SmtpConfigurationDto> UpdateAsync(Guid id, SmtpConfigurationRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<SmtpConfigurationDto> SetActiveAsync(Guid id, CancellationToken ct = default);
    Task<(bool Success, string Message)> TestConfigurationAsync(Guid id, string testEmail, CancellationToken ct = default);
}
