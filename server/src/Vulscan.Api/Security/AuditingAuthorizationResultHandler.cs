using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using System.Text.Json;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Security;

/// <summary>
/// Records authorization failures for signed-in users (a valid session attempting something its role does not
/// permit), then defers to the framework's default handling. Anonymous 401s are not recorded — they are noise.
/// </summary>
public sealed class AuditingAuthorizationResultHandler(IAuditService audit) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true)
        {
            await audit.LogAsync(new AuditEntry(
                Action: "Access.Denied",
                EntityType: "Authorization",
                Details: JsonSerializer.Serialize(new { method = context.Request.Method, path = context.Request.Path.Value }),
                Outcome: AuditOutcome.Denied));
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
