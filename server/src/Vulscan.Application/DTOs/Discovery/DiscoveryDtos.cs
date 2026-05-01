using System.ComponentModel.DataAnnotations;

namespace Vulscan.Application.DTOs.Discovery;

/// <summary>
/// Request to list available Azure DevOps projects on a server using shared credentials.
/// </summary>
public record DiscoveryListRequest
{
    /// <summary>Server base URL e.g. https://devops.ishj.ae (optionally with /tfs).</summary>
    [Required] public string ServerUrl { get; init; } = string.Empty;
    [Required] public string Collection { get; init; } = string.Empty;
    [Required] public string Username { get; init; } = string.Empty;
    [Required] public string Password { get; init; } = string.Empty;
}

public record DiscoveredProjectDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    /// <summary>True if a Project record already exists in our DB for this AZ DevOps project.</summary>
    public bool AlreadyImported { get; init; }
}

public record DiscoveryListResponse
{
    public Guid InstanceId { get; init; }
    public required string ServerUrl { get; init; }
    public required string Collection { get; init; }
    public List<DiscoveredProjectDto> Projects { get; init; } = [];
}

/// <summary>
/// Import selected projects from an instance — projects inherit the instance's shared credentials.
/// </summary>
public record DiscoveryImportRequest
{
    [Required] public Guid InstanceId { get; init; }
    [Required] public List<string> AzureProjectIds { get; init; } = [];
    public string? DefaultBranch { get; init; }
}

public record DiscoveryImportResponse
{
    public int Imported { get; init; }
    public int Skipped { get; init; }
    public List<Guid> ProjectIds { get; init; } = [];
}
