namespace Vulscan.Api.Middleware;

/// <summary>Standard hardening headers for an API that returns only JSON/CSV and is never framed.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var h = context.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            // Swagger UI (Development only) needs inline scripts and cacheable assets; the API itself never does.
            if (!context.Request.Path.StartsWithSegments("/swagger"))
            {
                h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
                h["Cache-Control"] = "no-store";   // responses contain security findings and tokens
            }
            return Task.CompletedTask;
        });

        return next(context);
    }
}
