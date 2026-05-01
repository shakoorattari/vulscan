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
        var project = await dbContext.Set<Project>()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.IsEnabled, ct)
            ?? throw new InvalidOperationException($"Project {request.ProjectId} not found or disabled.");

        var hasRunning = await dbContext.Set<ScanRun>()
            .AnyAsync(s => s.ProjectId == request.ProjectId
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
            Message = $"Scan queued for project '{project.Name}'. Scan ID: {scanRun.Id}",
        };
    }

    public async Task<PagedResult<ScanRunDto>> GetScanHistoryAsync(
        int page, int pageSize, Guid? projectId = null, CancellationToken ct = default)
    {
        var query = dbContext.Set<ScanRun>()
            .Include(s => s.Project)
            .Include(s => s.TriggeredBy)
            .AsQueryable();

        if (projectId.HasValue)
            query = query.Where(s => s.ProjectId == projectId.Value);

        var ordered = query.OrderByDescending(s => s.StartedAt);

        var totalCount = await ordered.CountAsync(ct);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => MapToDto(s))
            .ToListAsync(ct);

        return new PagedResult<ScanRunDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<ScanRunDto?> GetScanByIdAsync(Guid id, CancellationToken ct = default)
    {
        var scan = await dbContext.Set<ScanRun>()
            .Include(s => s.Project)
            .Include(s => s.TriggeredBy)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        return scan is null ? null : MapToDto(scan);
    }

    private static ScanRunDto MapToDto(ScanRun s) => new()
    {
        Id = s.Id,
        ProjectId = s.ProjectId,
        ProjectName = s.Project?.Name,
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
        ErrorLog = s.ErrorLog,
    };
}
