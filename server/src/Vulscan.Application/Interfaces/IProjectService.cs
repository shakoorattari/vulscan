using Vulscan.Application.DTOs.Projects;
using Vulscan.Application.DTOs.Scans;

namespace Vulscan.Application.Interfaces;

public interface IProjectService
{
    Task<List<ProjectDto>> GetAllAsync(CancellationToken ct = default);
    Task<List<ProjectSummaryDto>> GetSummariesAsync(CancellationToken ct = default);
    Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectConfigurationDto?> GetConfigurationAsync(Guid id, CancellationToken ct = default);
    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);
    Task<ProjectDto?> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ProjectDto?> SetEnabledAsync(Guid id, bool enabled, CancellationToken ct = default);
    Task<TriggerScanResponse> TriggerScanAsync(Guid id, Guid userId, CancellationToken ct = default);
}
