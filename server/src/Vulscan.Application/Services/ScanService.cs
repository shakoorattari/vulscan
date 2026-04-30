using Microsoft.EntityFrameworkCore;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Scans;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;

namespace Vulscan.Application.Services;

public sealed class ScanService(DbContext dbContext) : IScanService
{
    public async Task<TriggerScanResponse> TriggerScanAsync(
        TriggerScanRequest request, Guid userId, CancellationToken ct = default)
    {
        var instance = await dbContext.Set<AzureDevOpsInstance>()
            .FirstOrDefaultAsync(i => i.Id == request.InstanceId && i.IsEnabled, ct)
            ?? throw new InvalidOperationException($"Azure DevOps instance with ID {request.InstanceId} not found or disabled.");

        // Prevent concurrent scans on the same instance
        var runningScan = await dbContext.Set<ScanRun>()
            .AnyAsync(s => s.InstanceId == request.InstanceId
                        && (s.Status == ScanStatus.Running || s.Status == ScanStatus.Queued), ct);

        if (runningScan)
            throw new InvalidOperationException("A scan is already running or queued for this instance.");

        var scanRun = new ScanRun
        {
            InstanceId = request.InstanceId,
            TriggeredByUserId = userId,
            Status = ScanStatus.Queued,
            StartedAt = DateTime.UtcNow
        };

        dbContext.Set<ScanRun>().Add(scanRun);
        await dbContext.SaveChangesAsync(ct);

        // NOTE: In a full implementation, this would enqueue a background job
        // (e.g., via IHostedService, Hangfire, or a message queue) that:
        // 1. Clones repositories from the Azure DevOps instance
        // 2. Runs Syft to generate SBOMs
        // 3. Runs Grype to scan SBOMs for vulnerabilities
        // 4. Persists results to the database
        // 5. Sends notifications if thresholds are exceeded

        return new TriggerScanResponse
        {
            ScanRunId = scanRun.Id,
            Status = scanRun.Status.ToString(),
            Message = $"Scan queued for instance '{instance.Name}'. Scan ID: {scanRun.Id}"
        };
    }

    public async Task<PagedResult<ScanRunDto>> GetScanHistoryAsync(
        int page, int pageSize, Guid? instanceId = null, CancellationToken ct = default)
    {
        var query = dbContext.Set<ScanRun>()
            .Include(s => s.Instance)
            .Include(s => s.TriggeredBy)
            .AsQueryable();

        if (instanceId.HasValue)
        {
            query = query.Where(s => s.InstanceId == instanceId.Value);
        }

        var orderedQuery = query.OrderByDescending(s => s.StartedAt);

        var totalCount = await orderedQuery.CountAsync(ct);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => MapToDto(s))
            .ToListAsync(ct);

        return new PagedResult<ScanRunDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ScanRunDto?> GetScanByIdAsync(Guid id, CancellationToken ct = default)
    {
        var scan = await dbContext.Set<ScanRun>()
            .Include(s => s.Instance)
            .Include(s => s.TriggeredBy)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        return scan is null ? null : MapToDto(scan);
    }

    private static ScanRunDto MapToDto(ScanRun s) => new()
    {
        Id = s.Id,
        InstanceId = s.InstanceId,
        InstanceName = s.Instance?.Name,
        StartedAt = s.StartedAt,
        CompletedAt = s.CompletedAt,
        DurationSeconds = s.DurationSeconds,
        Status = s.Status.ToString(),
        ReposScanned = s.ReposScanned,
        ReposFailed = s.ReposFailed,
        TotalVulnerabilities = s.TotalVulnerabilities,
        CriticalCount = s.CriticalCount,
        HighCount = s.HighCount,
        MediumCount = s.MediumCount,
        LowCount = s.LowCount,
        TriggeredBy = s.TriggeredBy?.Username ?? "System",
        ErrorLog = s.ErrorLog
    };
}
