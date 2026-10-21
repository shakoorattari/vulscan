using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Filters;

/// <summary>
/// Records every state-changing API call (POST/PUT/PATCH/DELETE) and every data export/download made by an
/// authenticated user. Only the method, route and outcome are recorded — never request bodies, which can
/// carry credentials. Authentication events are audited explicitly by <c>AuthService</c> instead.
/// </summary>
public sealed class AuditActionFilter(IAuditService audit) : IAsyncActionFilter
{
    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase) { "Auth", "Health" };
    private static readonly string[] IdRouteKeys =
        ["id", "projectId", "scanRunId", "scanId", "instanceId", "repositoryId", "branchId", "cveId", "userId", "exceptionId"];

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        var http = context.HttpContext;
        var controller = context.RouteData.Values["controller"]?.ToString() ?? "Unknown";
        if (Skipped.Contains(controller) || http.User.Identity?.IsAuthenticated != true) return;

        var method = http.Request.Method;
        var path = http.Request.Path.Value ?? "";
        var mutating = HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
                       || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
        var export = HttpMethods.IsGet(method)
                     && (path.Contains("/export", StringComparison.OrdinalIgnoreCase)
                         || path.Contains("/download", StringComparison.OrdinalIgnoreCase));
        if (!mutating && !export) return;

        var status = executed.Exception is not null && !executed.ExceptionHandled
            ? StatusCodes.Status500InternalServerError
            : executed.Result switch
            {
                IStatusCodeActionResult { StatusCode: { } code } => code,
                _ => StatusCodes.Status200OK,
            };

        var outcome = status switch
        {
            >= 200 and < 400 => AuditOutcome.Success,
            StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden => AuditOutcome.Denied,
            _ => AuditOutcome.Failure,
        };

        var action = export ? $"{controller}.Export" : $"{controller}.{context.RouteData.Values["action"]}";
        var entityId = IdRouteKeys
            .Select(k => context.RouteData.Values.TryGetValue(k, out var v) ? v?.ToString() : null)
            .FirstOrDefault(v => !string.IsNullOrEmpty(v));

        await audit.LogAsync(new AuditEntry(
            Action: action,
            EntityType: controller,
            EntityId: entityId,
            Details: JsonSerializer.Serialize(new { method, path, status }),
            Outcome: outcome));
    }
}
