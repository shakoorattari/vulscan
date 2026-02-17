using Vulscan.Application.DTOs.Instances;

namespace Vulscan.Application.Interfaces;

/// <summary>
/// Service for managing Azure DevOps instance configurations.
/// </summary>
public interface IInstanceService
{
    /// <summary>
    /// Get all configured instances.
    /// </summary>
    Task<List<InstanceDto>> GetAllAsync();

    /// <summary>
    /// Get instance summaries for dropdowns.
    /// </summary>
    Task<List<InstanceSummaryDto>> GetSummariesAsync();

    /// <summary>
    /// Get instance by ID.
    /// </summary>
    Task<InstanceDto?> GetByIdAsync(int id);

    /// <summary>
    /// Create a new Azure DevOps instance from a project URL.
    /// </summary>
    Task<InstanceDto> CreateAsync(CreateInstanceRequest request);

    /// <summary>
    /// Update an existing instance.
    /// </summary>
    Task<InstanceDto?> UpdateAsync(int id, UpdateInstanceRequest request);

    /// <summary>
    /// Delete an instance.
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Test connection to an instance.
    /// </summary>
    Task<(bool Success, string Message)> TestConnectionAsync(int id);
}
