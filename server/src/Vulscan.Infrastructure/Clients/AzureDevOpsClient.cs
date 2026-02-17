using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vulscan.Application.Interfaces;

namespace Vulscan.Infrastructure.Clients;

/// <summary>
/// HTTP client for Azure DevOps REST API.
/// </summary>
public sealed class AzureDevOpsClient(HttpClient httpClient, ILogger<AzureDevOpsClient> logger) : IAzureDevOpsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<(bool Success, string Message)> TestConnectionAsync(
        string baseUrl, string collection, string username, string password, CancellationToken ct = default)
    {
        try
        {
            var url = $"{baseUrl}/{collection}/_apis/projects?api-version=6.0";
            using var request = CreateRequest(HttpMethod.Get, url, username, password);
            using var response = await httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(content);
                var count = doc.RootElement.TryGetProperty("count", out var c) ? c.GetInt32() : 0;
                return (true, $"Connected successfully. Found {count} project(s).");
            }

            return (false, $"Connection failed: {response.StatusCode} - {response.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to Azure DevOps at {BaseUrl}", baseUrl);
            return (false, $"Connection error: {ex.Message}");
        }
    }

    public async Task<List<AzureDevOpsProject>> GetProjectsAsync(
        string baseUrl, string collection, string username, string password, CancellationToken ct = default)
    {
        var projects = new List<AzureDevOpsProject>();

        try
        {
            var url = $"{baseUrl}/{collection}/_apis/projects?api-version=6.0";
            using var request = CreateRequest(HttpMethod.Get, url, username, password);
            using var response = await httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to get projects: {Status}", response.StatusCode);
                return projects;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("value", out var value))
            {
                foreach (var proj in value.EnumerateArray())
                {
                    var id = proj.GetProperty("id").GetString() ?? "";
                    var name = proj.GetProperty("name").GetString() ?? "";
                    var description = proj.TryGetProperty("description", out var desc) ? desc.GetString() : null;
                    var state = proj.TryGetProperty("state", out var s) ? s.GetString() ?? "wellFormed" : "wellFormed";

                    projects.Add(new AzureDevOpsProject(id, name, description, state));
                }
            }

            logger.LogInformation("Found {Count} projects in collection {Collection}", projects.Count, collection);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching projects from {Collection}", collection);
        }

        return projects;
    }

    public async Task<List<AzureDevOpsRepo>> GetRepositoriesAsync(
        string baseUrl, string collection, string project, string username, string password, CancellationToken ct = default)
    {
        var repos = new List<AzureDevOpsRepo>();

        try
        {
            var url = $"{baseUrl}/{collection}/{project}/_apis/git/repositories?api-version=6.0";
            using var request = CreateRequest(HttpMethod.Get, url, username, password);
            using var response = await httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to get repositories: {Status}", response.StatusCode);
                return repos;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("value", out var value))
            {
                foreach (var repo in value.EnumerateArray())
                {
                    var name = repo.GetProperty("name").GetString() ?? "";
                    var id = repo.GetProperty("id").GetString() ?? "";
                    var defaultBranch = repo.TryGetProperty("defaultBranch", out var db)
                        ? db.GetString()?.Replace("refs/heads/", "") ?? "main"
                        : "main";
                    var remoteUrl = repo.TryGetProperty("remoteUrl", out var ru) ? ru.GetString() ?? "" : "";
                    var size = repo.TryGetProperty("size", out var s) ? s.GetInt64() : 0;

                    repos.Add(new AzureDevOpsRepo(id, name, defaultBranch, remoteUrl, size));
                }
            }

            logger.LogInformation("Found {Count} repositories in {Project}", repos.Count, project);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching repositories from {Project}", project);
        }

        return repos;
    }

    public async Task<string?> GetFileContentAsync(
        string baseUrl, string collection, string project, string repoName, string filePath, string branch,
        string username, string password, CancellationToken ct = default)
    {
        try
        {
            var encodedPath = Uri.EscapeDataString(filePath);
            // Add $format=text to get raw file content instead of metadata
            var url = $"{baseUrl}/{collection}/{project}/_apis/git/repositories/{repoName}/items" +
                      $"?path={encodedPath}&versionDescriptor.version={branch}&$format=text&api-version=6.0";

            using var request = CreateRequest(HttpMethod.Get, url, username, password);
            using var response = await httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync(ct);
            }

            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                logger.LogWarning("Failed to get file {Path} from {Repo}: {Status}",
                    filePath, repoName, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching file {Path} from {Repo}", filePath, repoName);
        }

        return null;
    }

    public async Task<List<AzureDevOpsItem>> GetItemsAsync(
        string baseUrl, string collection, string project, string repoName, string path, string branch,
        string username, string password, CancellationToken ct = default)
    {
        var items = new List<AzureDevOpsItem>();

        try
        {
            var encodedPath = string.IsNullOrEmpty(path) ? "/" : Uri.EscapeDataString(path);
            var url = $"{baseUrl}/{collection}/{project}/_apis/git/repositories/{repoName}/items" +
                      $"?scopePath={encodedPath}&recursionLevel=Full&versionDescriptor.version={branch}&api-version=6.0";

            using var request = CreateRequest(HttpMethod.Get, url, username, password);
            using var response = await httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to list items at {Path} in {Repo}: {Status}",
                    path, repoName, response.StatusCode);
                return items;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("value", out var value))
            {
                foreach (var item in value.EnumerateArray())
                {
                    var itemPath = item.GetProperty("path").GetString() ?? "";
                    var gitObjectType = item.TryGetProperty("gitObjectType", out var got)
                        ? got.GetString() ?? "blob"
                        : "blob";
                    var size = item.TryGetProperty("size", out var s) ? s.GetInt64() : 0;

                    items.Add(new AzureDevOpsItem(itemPath, gitObjectType, size));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing items at {Path} in {Repo}", path, repoName);
        }

        return items;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string username, string password)
    {
        var request = new HttpRequestMessage(method, url);
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }
}
