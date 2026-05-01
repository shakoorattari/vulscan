namespace Vulscan.Application.DTOs.Instances;

/// <summary>
/// Azure DevOps server (URL + collection). Hosts one or more Projects.
/// Optionally stores shared credentials used by the Discovery flow; per-project
/// credentials always take precedence.
/// </summary>
public record InstanceDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public required string Collection { get; init; }
    public required string AuthMethod { get; init; }
    public bool IsEnabled { get; init; }
    public bool HasSharedCredentials { get; init; }
    public int ProjectCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Brief instance info for dropdowns.
/// </summary>
public record InstanceSummaryDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public required string Collection { get; init; }
    public bool IsEnabled { get; init; }
}

/// <summary>
/// Update an existing instance (rename, toggle, refresh shared discovery creds).
/// </summary>
public record UpdateInstanceRequest
{
    public required string Name { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public bool IsEnabled { get; init; } = true;
}
