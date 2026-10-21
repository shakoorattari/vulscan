using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Vulscan.Application.Auth;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;
using Vulscan.Infrastructure.Data;

namespace Vulscan.Infrastructure.BackgroundServices;

/// <summary>
/// Removal of unused access: deactivates accounts with no sign-in for <see cref="AuthOptions.DormantDays"/> days.
/// Never deactivates the last active administrator (that would lock everyone out), and reports it instead.
/// </summary>
public sealed class DormantAccountSweeper(
    IServiceScopeFactory scopeFactory,
    AuthOptions options,
    TimeProvider time,
    ILogger<DormantAccountSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.DormantDays <= 0)
        {
            logger.LogInformation("Dormant-account sweep disabled (Auth:DormantDays = 0)");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Dormant-account sweep failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task<int> SweepAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VulscanDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

        var now = time.GetUtcNow().UtcDateTime;
        var cutoff = now.AddDays(-options.DormantDays);

        // Last activity = the later of the last sign-in and when access was (re)granted (falls back to creation).
        // Filtered in memory: the user table is small and this avoids provider-specific date arithmetic in SQL.
        var dormant = (await db.Users.Where(u => u.IsActive).ToListAsync(ct))
            .Where(u => Latest(u.LastLoginAt, u.AccessGrantedAt ?? u.CreatedAt) < cutoff)
            .ToList();
        if (dormant.Count == 0) return 0;

        var activeAdmins = await db.Users.CountAsync(u => u.IsActive && u.Role == UserRole.Admin, ct);
        var deactivated = 0;

        foreach (var user in dormant)
        {
            if (user.Role == UserRole.Admin)
            {
                if (activeAdmins <= 1)
                {
                    logger.LogWarning("Admin {User} is dormant but is the last active administrator; not deactivating.", user.Username);
                    continue;
                }
                activeAdmins--;
            }

            user.IsActive = false;
            user.DeactivatedAt = now;
            user.DeactivationReason = $"Automatically deactivated: no sign-in for {options.DormantDays} days.";

            foreach (var t in await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct))
            {
                t.RevokedAt = now;
                t.RevokedReason = "account deactivated";
            }

            await db.SaveChangesAsync(ct);
            await audit.LogAsync(new AuditEntry("User.AutoDeactivated", "User", user.Id.ToString(),
                Details: JsonSerializer.Serialize(new { reason = "dormant", days = options.DormantDays, lastLogin = user.LastLoginAt }),
                UserId: user.Id, Username: user.Username), ct);
            deactivated++;
        }

        if (deactivated > 0)
            logger.LogWarning("Deactivated {Count} dormant account(s)", deactivated);

        return deactivated;
    }

    private static DateTime Latest(DateTime? a, DateTime b) => a is { } x && x > b ? x : b;
}
