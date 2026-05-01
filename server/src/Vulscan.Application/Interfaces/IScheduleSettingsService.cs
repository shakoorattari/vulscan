using Vulscan.Application.DTOs.Settings;

namespace Vulscan.Application.Interfaces;

public interface IScheduleSettingsService
{
    Task<ScheduleSettingsDto> GetAsync(CancellationToken ct = default);
    Task<ScheduleSettingsDto> UpdateAsync(UpdateScheduleSettingsRequest request, CancellationToken ct = default);
}
