using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Scans;

namespace Vulscan.Application.Interfaces;

public interface IScanService
{
    Task<TriggerScanResponse> TriggerScanAsync(TriggerScanRequest request, Guid userId, CancellationToken ct = default);
    Task<PagedResult<ScanRunDto>> GetScanHistoryAsync(int page, int pageSize, Guid? projectId = null, CancellationToken ct = default);
    Task<ScanRunDto?> GetScanByIdAsync(Guid id, CancellationToken ct = default);
}
