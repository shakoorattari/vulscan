using Vulscan.Application.DTOs.Common;
using Vulscan.Application.DTOs.Scans;

namespace Vulscan.Application.Interfaces;

public interface IScanService
{
    Task<TriggerScanResponse> TriggerScanAsync(TriggerScanRequest request, int userId, CancellationToken ct = default);
    Task<PagedResult<ScanRunDto>> GetScanHistoryAsync(int page, int pageSize, CancellationToken ct = default);
    Task<ScanRunDto?> GetScanByIdAsync(int id, CancellationToken ct = default);
}
