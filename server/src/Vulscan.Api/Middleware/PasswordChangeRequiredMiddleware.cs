using System.Text.Json;
using Vulscan.Application.DTOs.Common;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Middleware;

/// <summary>
/// While a user holds a token flagged <c>must_change_password</c> (first sign-in with the seeded or an
/// admin-issued password), everything except changing the password, refreshing or signing out is refused —
/// enforced here on the server, not merely hidden in the UI.
/// </summary>
public sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    public const string ErrorCode = "PASSWORD_CHANGE_REQUIRED";

    private static readonly string[] Allowed =
    [
        "/api/v1/auth/change-password", "/api/v1/auth/logout", "/api/v1/auth/refresh", "/api/v1/auth/config", "/api/v1/health",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.HasClaim(IJwtTokenService.MustChangePasswordClaim, "true")
            && !Allowed.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(
                ApiResponse.Fail("You must change your password before continuing.", [ErrorCode]),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            return;
        }

        await next(context);
    }
}
