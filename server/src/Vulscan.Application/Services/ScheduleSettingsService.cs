using Microsoft.EntityFrameworkCore;
using Vulscan.Application.Common;
using Vulscan.Application.DTOs.Settings;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;

namespace Vulscan.Application.Services;

public sealed class ScheduleSettingsService(DbContext dbContext) : IScheduleSettingsService
{
    public async Task<ScheduleSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var entity = await GetOrCreateAsync(ct);
        return MapToDto(entity);
    }

    public async Task<ScheduleSettingsDto> UpdateAsync(UpdateScheduleSettingsRequest request, CancellationToken ct = default)
    {
        // Validate cron up-front
        _ = CronExpressionHelper.ParseOrThrow(request.CronExpression);

        var entity = await GetOrCreateAsync(ct);
        entity.CronExpression = request.CronExpression.Trim();
        entity.Enabled = request.Enabled;
        entity.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return MapToDto(entity);
    }

    private async Task<ScheduleSettings> GetOrCreateAsync(CancellationToken ct)
    {
        var entity = await dbContext.Set<ScheduleSettings>()
            .FirstOrDefaultAsync(s => s.Id == ScheduleSettings.SingletonId, ct);

        if (entity is null)
        {
            entity = new ScheduleSettings
            {
                Id = ScheduleSettings.SingletonId,
                CronExpression = "0 2 * * *",
                Enabled = true,
                CreatedAt = DateTime.UtcNow,
            };
            dbContext.Set<ScheduleSettings>().Add(entity);
            await dbContext.SaveChangesAsync(ct);
        }

        return entity;
    }

    private static ScheduleSettingsDto MapToDto(ScheduleSettings s) => new()
    {
        CronExpression = s.CronExpression,
        CronDescription = CronExpressionHelper.Describe(s.CronExpression),
        Enabled = s.Enabled,
        NextRunUtc = CronExpressionHelper.NextOccurrence(s.CronExpression, DateTime.UtcNow),
        UpdatedAt = s.UpdatedAt ?? s.CreatedAt,
    };
}
