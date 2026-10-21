using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Infrastructure.Data;

namespace Vulscan.Infrastructure.Services;

/// <summary>
/// Writes audit records using its own DI scope (and therefore its own <see cref="VulscanDbContext"/>), so an
/// audit write is independent of the request's unit of work: a failed or half-tracked request transaction
/// cannot suppress the record of what happened, and an audit failure cannot fail the request.
/// </summary>
public sealed class AuditService(
    IServiceScopeFactory scopeFactory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditService> logger) : IAuditService
{
    private const int MaxDetailsLength = 4000;

    public async Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        try
        {
            var http = httpContextAccessor.HttpContext;
            var principal = http?.User is { Identity.IsAuthenticated: true } user ? user : null;

            var userId = entry.UserId;
            if (userId is null && principal is not null)
            {
                var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
                if (Guid.TryParse(sub, out var parsed)) userId = parsed;
            }

            var username = entry.Username
                ?? principal?.FindFirstValue(ClaimTypes.Name)
                ?? principal?.FindFirstValue("unique_name");

            var details = entry.Details;
            if (details is { Length: > MaxDetailsLength }) details = details[..MaxDetailsLength];

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VulscanDbContext>();
            db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Username = Truncate(username, 256),
                Action = Truncate(entry.Action, 200)!,
                EntityType = Truncate(entry.EntityType, 100)!,
                EntityId = Truncate(entry.EntityId, 100),
                Details = details,
                Outcome = entry.Outcome,
                IpAddress = Truncate(entry.IpAddress ?? http?.Connection.RemoteIpAddress?.ToString(), 50),
                Timestamp = DateTime.UtcNow,
            });
            // Deliberately not tied to the request's token: a client disconnect must not erase the record.
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Auditing must never break the audited operation — but a gap in the trail must be visible.
            logger.LogError(ex, "AUDIT WRITE FAILED for action {Action} on {EntityType}", entry.Action, entry.EntityType);
        }
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
