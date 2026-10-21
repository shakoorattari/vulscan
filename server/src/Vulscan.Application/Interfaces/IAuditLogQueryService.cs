using Vulscan.Application.DTOs.Audit;
using Vulscan.Application.DTOs.Common;

namespace Vulscan.Application.Interfaces;

public interface IAuditLogQueryService
{
    Task<PagedResult<AuditLogDto>> GetPageAsync(AuditLogFilterDto filter, CancellationToken ct = default);

    /// <summary>Up to <paramref name="maxRows"/> matching records, newest first, as CSV.</summary>
    Task<string> ExportCsvAsync(AuditLogFilterDto filter, int maxRows = 50_000, CancellationToken ct = default);
}
