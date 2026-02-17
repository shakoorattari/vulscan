namespace Vulscan.Application.Interfaces;

/// <summary>
/// Client for interacting with Azure DevOps REST API.
/// </summary>
public interface IAzureDevOpsClient
{
    /// <summary>
    /// Tests connectivity to the Azure DevOps instance.
    /// </summary>
    Task<(bool Success, string Message)> TestConnectionAsync(
        string baseUrl, string collection, string username, string password, CancellationToken ct = default);

    /// <summary>
    /// Gets all projects in the collection.
    /// </summary>
    Task<List<AzureDevOpsProject>> GetProjectsAsync(
        string baseUrl, string collection, string username, string password, CancellationToken ct = default);

    /// <summary>
    /// Gets all repositories for a project.
    /// </summary>
    Task<List<AzureDevOpsRepo>> GetRepositoriesAsync(
        string baseUrl, string collection, string project, string username, string password, CancellationToken ct = default);

    /// <summary>
    /// Gets file content from a repository.
    /// </summary>
    Task<string?> GetFileContentAsync(
        string baseUrl, string collection, string project, string repoName, string filePath, string branch,
        string username, string password, CancellationToken ct = default);

    /// <summary>
    /// Lists files in a repository at a specific path.
    /// </summary>
    Task<List<AzureDevOpsItem>> GetItemsAsync(
        string baseUrl, string collection, string project, string repoName, string path, string branch,
        string username, string password, CancellationToken ct = default);
}

/// <summary>
/// Represents a repository from Azure DevOps.
/// </summary>
public record AzureDevOpsRepo(
    string Id,
    string Name,
    string DefaultBranch,
    string RemoteUrl,
    long Size);

/// <summary>
/// Represents a file or folder item from Azure DevOps.
/// </summary>
public record AzureDevOpsItem(
    string Path,
    string GitObjectType, // "blob" or "tree"
    long Size);

/// <summary>
/// Represents a project from Azure DevOps.
/// </summary>
public record AzureDevOpsProject(
    string Id,
    string Name,
    string? Description,
    string State);
