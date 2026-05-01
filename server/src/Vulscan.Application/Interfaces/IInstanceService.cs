using Vulscan.Application.DTOs.Instances;

namespace Vulscan.Application.Interfaces;

/// <summary>
/// Read/manage Azure DevOps server (URL + collection) configurations.
/// Project creation lives on <see cref="IProjectService"/>.
/// </summary>
public interface IInstanceService
{
    Task<List<InstanceDto>> GetAllAsync();
    Task<List<InstanceSummaryDto>> GetSummariesAsync();
    Task<InstanceDto?> GetByIdAsync(Guid id);
    Task<InstanceDto?> UpdateAsync(Guid id, UpdateInstanceRequest request);
    Task<bool> DeleteAsync(Guid id);
    Task<(bool Success, string Message)> TestConnectionAsync(Guid id);
}
