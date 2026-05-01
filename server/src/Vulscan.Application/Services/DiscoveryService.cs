using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulscan.Application.DTOs.Discovery;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;

namespace Vulscan.Application.Services;

public sealed class DiscoveryService(
    DbContext dbContext,
    IAzureDevOpsClient azureDevOpsClient) : IDiscoveryService
{
    public async Task<DiscoveryListResponse> ListProjectsAsync(
        DiscoveryListRequest request, CancellationToken ct = default)
    {
        var serverUrl = request.ServerUrl.TrimEnd('/');
        var collection = request.Collection.Trim();

        var (ok, msg) = await azureDevOpsClient.TestConnectionAsync(
            serverUrl, collection, request.Username, request.Password, ct);
        if (!ok)
            throw new InvalidOperationException($"Connection failed: {msg}");

        var azureProjects = await azureDevOpsClient.GetProjectsAsync(
            serverUrl, collection, request.Username, request.Password, ct);

        // Find or create instance with shared creds
        var instance = await dbContext.Set<AzureDevOpsInstance>()
            .Include(i => i.Projects)
            .FirstOrDefaultAsync(i => i.Url == serverUrl && i.Collection == collection, ct);

        var sharedCreds = JsonSerializer.Serialize(new
        {
            username = request.Username,
            password = request.Password,
        });

        if (instance is null)
        {
            instance = new AzureDevOpsInstance
            {
                Name = $"{new Uri(serverUrl).Host}/{collection}",
                Url = serverUrl,
                Collection = collection,
                AuthMethod = AuthMethod.BasicAuth,
                CredentialReference = sharedCreds,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
            };
            dbContext.Set<AzureDevOpsInstance>().Add(instance);
        }
        else
        {
            instance.CredentialReference = sharedCreds;
            instance.UpdatedAt = DateTime.UtcNow;
        }
        await dbContext.SaveChangesAsync(ct);

        var existing = instance.Projects
            .Select(p => p.AzureProjectId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new DiscoveryListResponse
        {
            InstanceId = instance.Id,
            ServerUrl = serverUrl,
            Collection = collection,
            Projects = [.. azureProjects.Select(p => new DiscoveredProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                AlreadyImported = existing.Contains(p.Name) || existing.Contains(p.Id),
            })],
        };
    }

    public async Task<DiscoveryImportResponse> ImportProjectsAsync(
        DiscoveryImportRequest request, CancellationToken ct = default)
    {
        var instance = await dbContext.Set<AzureDevOpsInstance>()
            .Include(i => i.Projects)
            .FirstOrDefaultAsync(i => i.Id == request.InstanceId, ct)
            ?? throw new InvalidOperationException($"Instance {request.InstanceId} not found.");

        if (string.IsNullOrEmpty(instance.CredentialReference))
            throw new InvalidOperationException("Instance has no shared credentials configured. Run discovery first.");

        // Re-fetch projects to validate names and get IDs
        var creds = ParseCreds(instance.CredentialReference);
        var azureProjects = await azureDevOpsClient.GetProjectsAsync(
            instance.Url, instance.Collection, creds.Username, creds.Password, ct);

        int imported = 0, skipped = 0;
        var newIds = new List<Guid>();

        foreach (var azProjectIdOrName in request.AzureProjectIds)
        {
            var azProject = azureProjects.FirstOrDefault(p =>
                p.Id.Equals(azProjectIdOrName, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals(azProjectIdOrName, StringComparison.OrdinalIgnoreCase));

            if (azProject is null)
            {
                skipped++;
                continue;
            }

            if (instance.Projects.Any(p => p.AzureProjectId.Equals(azProject.Name, StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
                continue;
            }

            var url = $"{instance.Url}/{instance.Collection}/{azProject.Name}";
            var project = new Project
            {
                InstanceId = instance.Id,
                Name = azProject.Name,
                AzureProjectId = azProject.Name,
                Url = url,
                CredentialReference = null, // inherit from instance
                DefaultBranch = string.IsNullOrWhiteSpace(request.DefaultBranch) ? null : request.DefaultBranch,
                IsEnabled = true,
                DiscoveredAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            dbContext.Set<Project>().Add(project);
            instance.Projects.Add(project);
            imported++;
        }

        await dbContext.SaveChangesAsync(ct);
        newIds.AddRange(instance.Projects
            .Where(p => p.CreatedAt >= DateTime.UtcNow.AddSeconds(-5))
            .Select(p => p.Id));

        return new DiscoveryImportResponse { Imported = imported, Skipped = skipped, ProjectIds = newIds };
    }

    private static (string Username, string Password) ParseCreds(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return (
                root.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "",
                root.TryGetProperty("password", out var p) ? p.GetString() ?? "" : ""
            );
        }
        catch
        {
            return ("", "");
        }
    }
}
