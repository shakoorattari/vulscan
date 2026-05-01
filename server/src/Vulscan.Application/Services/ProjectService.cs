using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulscan.Application.Common;
using Vulscan.Application.DTOs.Projects;
using Vulscan.Application.DTOs.Scans;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;

namespace Vulscan.Application.Services;

public sealed class ProjectService(DbContext dbContext) : IProjectService
{
    private async Task<string> GetGlobalCronAsync(CancellationToken ct)
    {
        var s = await dbContext.Set<ScheduleSettings>()
            .Where(x => x.Id == ScheduleSettings.SingletonId)
            .Select(x => x.CronExpression)
            .FirstOrDefaultAsync(ct);
        return s ?? "0 2 * * *";
    }

    private static string? NormalizeCron(string? expr)
    {
        if (string.IsNullOrWhiteSpace(expr)) return null;
        var trimmed = expr.Trim();
        _ = CronExpressionHelper.ParseOrThrow(trimmed); // throws on invalid
        return trimmed;
    }
    public async Task<List<ProjectDto>> GetAllAsync(CancellationToken ct = default)
    {
        var globalCron = await GetGlobalCronAsync(ct);
        var projects = await dbContext.Set<Project>()
            .Include(p => p.Instance)
            .Include(p => p.Repositories)
            .Include(p => p.ScanRuns)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return [.. projects.Select(p => MapToDto(p, globalCron))];
    }

    public async Task<List<ProjectSummaryDto>> GetSummariesAsync(CancellationToken ct = default)
    {
        var projects = await dbContext.Set<Project>()
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Name)
            .Select(p => new ProjectSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                Url = p.Url,
                IsEnabled = p.IsEnabled,
            })
            .ToListAsync(ct);

        return projects;
    }

    public async Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await dbContext.Set<Project>()
            .Include(p => p.Instance)
            .Include(p => p.Repositories)
            .Include(p => p.ScanRuns)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return project is null ? null : MapToDto(project, await GetGlobalCronAsync(ct));
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var (baseUrl, collection, projectName) = AzureDevOpsUrlParser.Parse(request.ProjectUrl);

        // Find or create the parent instance (server URL + collection)
        var instance = await dbContext.Set<AzureDevOpsInstance>()
            .Include(i => i.Projects)
            .FirstOrDefaultAsync(i => i.Url == baseUrl && i.Collection == collection, ct);

        if (instance is null)
        {
            instance = new AzureDevOpsInstance
            {
                Name = $"{new Uri(baseUrl).Host}/{collection}",
                Url = baseUrl,
                Collection = collection,
                AuthMethod = AuthMethod.BasicAuth,
                CredentialReference = string.Empty,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
            };
            dbContext.Set<AzureDevOpsInstance>().Add(instance);
            await dbContext.SaveChangesAsync(ct);
        }

        // Reject duplicate project under the same instance
        if (instance.Projects.Any(p => p.AzureProjectId.Equals(projectName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Project '{projectName}' already exists under {baseUrl}/{collection}.");

        var project = new Project
        {
            InstanceId = instance.Id,
            Name = request.Name,
            AzureProjectId = projectName,
            Url = request.ProjectUrl.TrimEnd('/'),
            CredentialReference = JsonSerializer.Serialize(new
            {
                username = request.Username,
                password = request.Password,
            }),
            DefaultBranch = string.IsNullOrWhiteSpace(request.DefaultBranch) ? null : request.DefaultBranch,
            CronExpression = NormalizeCron(request.CronExpression),
            IsEnabled = true,
            DiscoveredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.Set<Project>().Add(project);
        await dbContext.SaveChangesAsync(ct);

        await dbContext.Entry(project).Reference(p => p.Instance).LoadAsync(ct);
        return MapToDto(project, await GetGlobalCronAsync(ct));
    }

    public async Task<ProjectDto?> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var project = await dbContext.Set<Project>()
            .Include(p => p.Instance)
            .Include(p => p.Repositories)
            .Include(p => p.ScanRuns)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null) return null;

        project.Name = request.Name;
        project.IsEnabled = request.IsEnabled;
        project.DefaultBranch = string.IsNullOrWhiteSpace(request.DefaultBranch) ? null : request.DefaultBranch;
        project.CronExpression = NormalizeCron(request.CronExpression);
        project.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(request.Username) && !string.IsNullOrEmpty(request.Password))
        {
            project.CredentialReference = JsonSerializer.Serialize(new
            {
                username = request.Username,
                password = request.Password,
            });
        }

        await dbContext.SaveChangesAsync(ct);
        return MapToDto(project, await GetGlobalCronAsync(ct));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await dbContext.Set<Project>().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return false;

        dbContext.Set<Project>().Remove(project);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ProjectDto?> SetEnabledAsync(Guid id, bool enabled, CancellationToken ct = default)
    {
        var project = await dbContext.Set<Project>()
            .Include(p => p.Instance)
            .Include(p => p.Repositories)
            .Include(p => p.ScanRuns)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return null;

        project.IsEnabled = enabled;
        project.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return MapToDto(project, await GetGlobalCronAsync(ct));
    }

    public async Task<TriggerScanResponse> TriggerScanAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var project = await dbContext.Set<Project>()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsEnabled, ct)
            ?? throw new InvalidOperationException($"Project {id} not found or disabled.");

        var hasRunning = await dbContext.Set<ScanRun>()
            .AnyAsync(s => s.ProjectId == id
                        && (s.Status == ScanStatus.Running || s.Status == ScanStatus.Queued), ct);

        if (hasRunning)
            throw new InvalidOperationException("A scan is already running or queued for this project.");

        var scanRun = new ScanRun
        {
            ProjectId = project.Id,
            TriggeredByUserId = userId,
            Status = ScanStatus.Queued,
            StartedAt = DateTime.UtcNow,
        };

        dbContext.Set<ScanRun>().Add(scanRun);
        await dbContext.SaveChangesAsync(ct);

        return new TriggerScanResponse
        {
            ScanRunId = scanRun.Id,
            Status = scanRun.Status.ToString(),
            Message = $"Scan queued for project '{project.Name}'.",
        };
    }

    private static ProjectDto MapToDto(Project p, string globalCron)
    {
        var lastScan = p.ScanRuns?.MaxBy(s => s.StartedAt);
        return new ProjectDto
        {
            Id = p.Id,
            InstanceId = p.InstanceId,
            InstanceName = p.Instance?.Name ?? "",
            Name = p.Name,
            AzureProjectId = p.AzureProjectId,
            Url = p.Url,
            DefaultBranch = p.DefaultBranch,
            IsEnabled = p.IsEnabled,
            HasOwnCredentials = !string.IsNullOrEmpty(p.CredentialReference),
            CronExpression = p.CronExpression,
            EffectiveCron = p.CronExpression ?? globalCron,
            CreatedAt = p.CreatedAt,
            LastScannedAt = p.LastScannedAt ?? lastScan?.StartedAt,
            RepositoryCount = p.Repositories?.Count ?? 0,
            TotalScans = p.ScanRuns?.Count ?? 0,
            TotalVulnerabilities = p.ScanRuns?.Sum(s => s.TotalVulnerabilities) ?? 0,
            LastScanId = lastScan?.Id,
            LastScanStatus = lastScan?.Status.ToString(),
            LastScanDurationSeconds = lastScan?.DurationSeconds,
            LastScanCriticalCount = lastScan?.CriticalCount ?? 0,
            LastScanHighCount = lastScan?.HighCount ?? 0,
            LastScanMediumCount = lastScan?.MediumCount ?? 0,
            LastScanLowCount = lastScan?.LowCount ?? 0,
            LastScanTotalVulnerabilities = lastScan?.TotalVulnerabilities ?? 0,
        };
    }
}
