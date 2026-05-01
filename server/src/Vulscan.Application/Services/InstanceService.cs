using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulscan.Application.DTOs.Instances;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;

namespace Vulscan.Application.Services;

public sealed class InstanceService(DbContext dbContext) : IInstanceService
{
    public async Task<List<InstanceDto>> GetAllAsync()
    {
        var instances = await dbContext.Set<AzureDevOpsInstance>()
            .Include(i => i.Projects)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return instances.Select(MapToDto).ToList();
    }

    public async Task<List<InstanceSummaryDto>> GetSummariesAsync()
    {
        var instances = await dbContext.Set<AzureDevOpsInstance>()
            .Where(i => i.IsEnabled)
            .OrderBy(i => i.Name)
            .ToListAsync();

        return [.. instances.Select(i => new InstanceSummaryDto
        {
            Id = i.Id,
            Name = i.Name,
            Url = i.Url,
            Collection = i.Collection,
            IsEnabled = i.IsEnabled,
        })];
    }

    public async Task<InstanceDto?> GetByIdAsync(Guid id)
    {
        var instance = await dbContext.Set<AzureDevOpsInstance>()
            .Include(i => i.Projects)
            .FirstOrDefaultAsync(i => i.Id == id);

        return instance is null ? null : MapToDto(instance);
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

        if (!string.IsNullOrEmpty(request.Username) || !string.IsNullOrEmpty(request.Password))
        {
            instance.CredentialReference = JsonSerializer.Serialize(new
            {
                username = request.Username ?? "",
                password = request.Password ?? "",
            });
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
            .FirstOrDefaultAsync(i => i.Id == id);

        if (instance is null)
            return (false, "Instance not found.");

        if (string.IsNullOrEmpty(instance.CredentialReference))
            return (false, "No shared credentials configured for this instance.");

        return (true, $"Configuration valid for {instance.Url}/{instance.Collection}");
    }

    private static InstanceDto MapToDto(AzureDevOpsInstance instance) => new()
    {
        Id = instance.Id,
        Name = instance.Name,
        Url = instance.Url,
        Collection = instance.Collection,
        AuthMethod = instance.AuthMethod.ToString(),
        IsEnabled = instance.IsEnabled,
        HasSharedCredentials = !string.IsNullOrEmpty(instance.CredentialReference),
        ProjectCount = instance.Projects?.Count ?? 0,
        CreatedAt = instance.CreatedAt,
    };
}
