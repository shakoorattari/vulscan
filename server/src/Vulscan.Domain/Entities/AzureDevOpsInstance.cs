using Vulscan.Domain.Common;
using Vulscan.Domain.Enums;

namespace Vulscan.Domain.Entities;

/// <summary>
/// Represents an on-premises Azure DevOps Server instance/collection.
/// </summary>
public class AzureDevOpsInstance : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Collection { get; set; } = string.Empty;
    public AuthMethod AuthMethod { get; set; } = AuthMethod.Pat;
    public string CredentialReference { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    // Navigation
    public ICollection<Project> Projects { get; set; } = [];
}
