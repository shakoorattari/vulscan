using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Vulscan.Application.DTOs.Instances;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;

namespace Vulscan.Application.Services;

public partial class InstanceService(DbContext dbContext) : IInstanceService
{
    public async Task<List<InstanceDto>> GetAllAsync()
    {
        var instances = await dbContext.Set<AzureDevOpsInstance>()
            .Include(i => i.Projects)
            .Include(i => i.ScanRuns)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return instances.Select(MapToDto).ToList();
    }

    public async Task<List<InstanceSummaryDto>> GetSummariesAsync()
    {
        var instances = await dbContext.Set<AzureDevOpsInstance>()
            .Include(i => i.Projects)
            .Where(i => i.IsEnabled)
            .OrderBy(i => i.Name)
            .ToListAsync();

        return instances.Select(i => new InstanceSummaryDto
        {
            Id = i.Id,
            Name = i.Name,
            ProjectName = i.Projects.FirstOrDefault()?.Name ?? "Unknown",
            IsEnabled = i.IsEnabled
        }).ToList();
    }

    public async Task<InstanceDto?> GetByIdAsync(Guid id)
    {
        var instance = await dbContext.Set<AzureDevOpsInstance>()
            .Include(i => i.Projects)
            .Include(i => i.ScanRuns)
            .FirstOrDefaultAsync(i => i.Id == id);

        return instance is null ? null : MapToDto(instance);
    }

    public async Task<InstanceDto> CreateAsync(CreateInstanceRequest request)
    {
        // Parse the Azure DevOps URL
        // Expected format: https://devops.ishj.ae/SDD/TransLynk
        // Or: https://devops.ishj.ae/tfs/SDD/TransLynk
        var (baseUrl, collection, projectName) = ParseAzureDevOpsUrl(request.ProjectUrl);

        // Create credentials JSON (in production, encrypt this!)
        var credentials = JsonSerializer.Serialize(new
        {
            username = request.Username,
            password = request.Password,  // TODO: Encrypt in production
            branch = request.Branch ?? "main"
        });

        var instance = new AzureDevOpsInstance
        {
            Name = request.Name,
            Url = baseUrl,
            Collection = collection,
            AuthMethod = AuthMethod.BasicAuth,
            CredentialReference = credentials,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Set<AzureDevOpsInstance>().Add(instance);
        await dbContext.SaveChangesAsync();

        // Create the project entry
        var project = new Project
        {
            InstanceId = instance.Id,
            Name = projectName,
            AzureProjectId = projectName,
            DiscoveredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Set<Project>().Add(project);
        await dbContext.SaveChangesAsync();

        // Reload with navigation properties
        await dbContext.Entry(instance).Collection(i => i.Projects).LoadAsync();

        return MapToDto(instance);
    }

    public async Task<InstanceDto?> UpdateAsync(Guid id, UpdateInstanceRequest request)
    {
        var instance = await dbContext.Set<AzureDevOpsInstance>()
            .Include(i => i.Projects)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (instance is null) return null;

        instance.Name = request.Name;
        instance.IsEnabled = request.IsEnabled;
        instance.UpdatedAt = DateTime.UtcNow;

        // Update credentials if provided
        if (!string.IsNullOrEmpty(request.Username) || !string.IsNullOrEmpty(request.Password))
        {
            var existingCreds = TryParseCredentials(instance.CredentialReference);
            var credentials = JsonSerializer.Serialize(new
            {
                username = request.Username ?? existingCreds.username,
                password = request.Password ?? existingCreds.password,
                branch = request.Branch ?? existingCreds.branch
            });
            instance.CredentialReference = credentials;
        }

        await dbContext.SaveChangesAsync();
        return MapToDto(instance);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var instance = await dbContext.Set<AzureDevOpsInstance>()
            .FirstOrDefaultAsync(i => i.Id == id);

        if (instance is null) return false;

        dbContext.Set<AzureDevOpsInstance>().Remove(instance);
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(Guid id)
    {
        var instance = await dbContext.Set<AzureDevOpsInstance>()
            .Include(i => i.Projects)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (instance is null)
            return (false, "Instance not found.");

        // TODO: Implement actual Azure DevOps API connection test
        // For now, just validate the configuration exists
        var creds = TryParseCredentials(instance.CredentialReference);
        if (string.IsNullOrEmpty(creds.username) || string.IsNullOrEmpty(creds.password))
            return (false, "Invalid credentials configuration.");

        return (true, $"Configuration valid for {instance.Url}/{instance.Collection}");
    }

    private static (string baseUrl, string collection, string projectName) ParseAzureDevOpsUrl(string url)
    {
        // Remove trailing slash
        url = url.TrimEnd('/');

        // Match URLs like:
        // https://devops.ishj.ae/SDD/TransLynk
        // https://devops.ishj.ae/tfs/DefaultCollection/MyProject
        // https://dev.azure.com/orgname/ProjectName
        var uri = new Uri(url);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2)
            throw new ArgumentException("Invalid Azure DevOps URL. Expected format: https://server/collection/project");

        string baseUrl = $"{uri.Scheme}://{uri.Host}";
        if (uri.Port != 80 && uri.Port != 443)
            baseUrl += $":{uri.Port}";

        // Check if URL contains 'tfs' segment
        int collectionIndex = 0;
        if (segments.Length > 2 && segments[0].Equals("tfs", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl += "/tfs";
            collectionIndex = 1;
        }

        string collection = segments[collectionIndex];
        string projectName = segments[collectionIndex + 1];

        return (baseUrl, collection, projectName);
    }

    private static (string username, string password, string branch) TryParseCredentials(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return (
                root.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "",
                root.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "",
                root.TryGetProperty("branch", out var b) ? b.GetString() ?? "main" : "main"
            );
        }
        catch
        {
            return ("", "", "main");
        }
    }

    private static InstanceDto MapToDto(AzureDevOpsInstance instance)
    {
        var lastScan = instance.ScanRuns?.MaxBy(s => s.StartedAt);
        var totalVulns = instance.ScanRuns?.Sum(s => s.TotalVulnerabilities) ?? 0;

        return new InstanceDto
        {
            Id = instance.Id,
            Name = instance.Name,
            Url = instance.Url,
            Collection = instance.Collection,
            ProjectName = instance.Projects?.FirstOrDefault()?.Name ?? "Unknown",
            AuthMethod = instance.AuthMethod.ToString(),
            IsEnabled = instance.IsEnabled,
            CreatedAt = instance.CreatedAt,
            LastScannedAt = lastScan?.StartedAt,
            TotalScans = instance.ScanRuns?.Count ?? 0,
            TotalVulnerabilities = totalVulns,
            LastScanId = lastScan?.Id,
            LastScanStatus = lastScan?.Status.ToString(),
            LastScanDurationSeconds = lastScan?.DurationSeconds,
            LastScanCriticalCount = lastScan?.CriticalCount ?? 0,
            LastScanHighCount = lastScan?.HighCount ?? 0,
            LastScanMediumCount = lastScan?.MediumCount ?? 0,
            LastScanLowCount = lastScan?.LowCount ?? 0,
            LastScanTotalVulnerabilities = lastScan?.TotalVulnerabilities ?? 0
        };
    }
}
