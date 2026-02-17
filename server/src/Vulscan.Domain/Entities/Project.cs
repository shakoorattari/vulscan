using Vulscan.Domain.Common;

namespace Vulscan.Domain.Entities;

/// <summary>
/// Azure DevOps project within a collection/instance.
/// </summary>
public class Project : BaseEntity
{
    public int InstanceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AzureProjectId { get; set; } = string.Empty;
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AzureDevOpsInstance Instance { get; set; } = null!;
    public ICollection<Repository> Repositories { get; set; } = [];
}
