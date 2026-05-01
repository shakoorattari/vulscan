using Vulscan.Application.DTOs.Discovery;

namespace Vulscan.Application.Interfaces;

public interface IDiscoveryService
{
    /// <summary>
    /// Connect to an Azure DevOps server using the supplied shared credentials, list all
    /// available projects, and persist the instance + shared creds (for later import).
    /// </summary>
    Task<DiscoveryListResponse> ListProjectsAsync(DiscoveryListRequest request, CancellationToken ct = default);

    /// <summary>
    /// Bulk-import selected projects on an instance. Each new project inherits the
    /// instance's shared credentials (Project.CredentialReference is left null).
    /// </summary>
    Task<DiscoveryImportResponse> ImportProjectsAsync(DiscoveryImportRequest request, CancellationToken ct = default);
}
