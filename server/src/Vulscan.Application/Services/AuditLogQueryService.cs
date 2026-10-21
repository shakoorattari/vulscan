using System.Text;
using Microsoft.EntityFrameworkCore;
using Vulscan.Application.Common;
using Vulscan.Application.DTOs.Audit;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;

namespace Vulscan.Application.Services;

public sealed class AuditLogQueryService(DbContext dbContext) : IAuditLogQueryService
{
    private const int MaxPageSize = 200;

    public async Task<PagedResult<AuditLogDto>> GetPageAsync(AuditLogFilterDto filter, CancellationToken ct = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, MaxPageSize);

        var query = Filter(filter);
        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => ToDto(a))
            .ToListAsync(ct);

        return new PagedResult<AuditLogDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<string> ExportCsvAsync(AuditLogFilterDto filter, int maxRows = 50_000, CancellationToken ct = default)
    {
        var rows = await Filter(filter)
            .OrderByDescending(a => a.Timestamp)
            .Take(maxRows)
            .Select(a => ToDto(a))
            .ToListAsync(ct);

        var csv = new StringBuilder();
        csv.AppendLine("Timestamp (UTC),User,Action,EntityType,EntityId,Outcome,IpAddress,Details");
        foreach (var r in rows)
            csv.AppendLine(Csv.Row(r.Timestamp, r.Username, r.Action, r.EntityType, r.EntityId, r.Outcome, r.IpAddress, r.Details));
        return csv.ToString();
    }

    private IQueryable<AuditLog> Filter(AuditLogFilterDto f)
    {
        var q = dbContext.Set<AuditLog>().AsNoTracking().AsQueryable();

        // Timestamps are stored in UTC. MVC binds "…Z" query values into LOCAL-kind DateTimes, so normalise first or
        // the bounds would shift by the server's UTC offset.
        if (f.From.HasValue) { var from = ToUtc(f.From.Value); q = q.Where(a => a.Timestamp >= from); }
        if (f.To.HasValue) { var to = ToUtc(f.To.Value); q = q.Where(a => a.Timestamp <= to); }
        if (!string.IsNullOrWhiteSpace(f.Action)) q = q.Where(a => a.Action.Contains(f.Action));
        if (!string.IsNullOrWhiteSpace(f.Username)) q = q.Where(a => a.Username != null && a.Username.Contains(f.Username));
        if (!string.IsNullOrWhiteSpace(f.EntityType)) q = q.Where(a => a.EntityType == f.EntityType);
        if (!string.IsNullOrWhiteSpace(f.Outcome)) q = q.Where(a => a.Outcome == f.Outcome);
        if (!string.IsNullOrWhiteSpace(f.Search))
            q = q.Where(a => a.Action.Contains(f.Search)
                             || (a.Details != null && a.Details.Contains(f.Search))
                             || (a.EntityId != null && a.EntityId.Contains(f.Search)));

        return q;
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    private static AuditLogDto ToDto(AuditLog a) => new()
    {
        Id = a.Id,
        Timestamp = a.Timestamp,
        UserId = a.UserId,
        Username = a.Username,
        Action = a.Action,
        EntityType = a.EntityType,
        EntityId = a.EntityId,
        Outcome = a.Outcome,
        IpAddress = a.IpAddress,
        Details = a.Details,
    };
}
