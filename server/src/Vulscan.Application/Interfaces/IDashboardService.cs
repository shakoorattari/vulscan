using Vulscan.Application.DTOs.Dashboard;

namespace Vulscan.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct = default);
}
