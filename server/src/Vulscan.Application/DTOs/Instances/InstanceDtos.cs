namespace Vulscan.Application.DTOs.Instances;

/// <summary>
/// DTO for creating a new Azure DevOps instance.
/// </summary>
public record CreateInstanceRequest
{
    /// <summary>
    /// Display name for this instance.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full URL to the Azure DevOps project (e.g., https://devops.ishj.ae/SDD/TransLynk).
    /// </summary>
    public required string ProjectUrl { get; init; }

    /// <summary>
    /// Username for authentication (e.g., email).
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Password or Personal Access Token for authentication.
    /// </summary>
    public required string Password { get; init; }

    /// <summary>
    /// Branch to scan (default: main or master).
    /// </summary>
    public string? Branch { get; init; }
}

/// <summary>
/// DTO for updating an existing instance.
/// </summary>
public record UpdateInstanceRequest
{
    public required string Name { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? Branch { get; init; }
    public bool IsEnabled { get; init; } = true;
}

/// <summary>
/// Response DTO for instance details.
/// </summary>
public record InstanceDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public required string Collection { get; init; }
    public required string ProjectName { get; init; }
    public required string AuthMethod { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastScannedAt { get; init; }
    public int TotalScans { get; init; }
    public int TotalVulnerabilities { get; init; }
}

/// <summary>
/// Brief instance info for dropdowns.
/// </summary>
public record InstanceSummaryDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string ProjectName { get; init; }
    public bool IsEnabled { get; init; }
}
