using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;
using Vulscan.Api.Filters;
using Vulscan.Api.Middleware;
using Vulscan.Api.Serialization;
using Vulscan.Api.Security;
using Vulscan.Application;
using Vulscan.Infrastructure;
using Vulscan.Infrastructure.Data;

// ---------------------------------------------------------------------------
// Serilog Bootstrap
// ---------------------------------------------------------------------------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Vulscan")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/vulscan-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting Vulscan API...");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog
    builder.Host.UseSerilog();

    // Don't advertise the server software.
    builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

    // ---------------------------------------------------------------------------
    // Service Registration
    // ---------------------------------------------------------------------------

    // Clean Architecture layers
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // HTTP context access for CurrentUserService
    builder.Services.AddHttpContextAccessor();

    // Controllers with JSON options
    builder.Services.AddControllers(options => options.Filters.Add<AuditActionFilter>())
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
            options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeConverter());
            options.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

    // JWT Authentication
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var secretKey = jwtSection["SecretKey"]
        ?? throw new InvalidOperationException("JWT SecretKey must be configured in appsettings.");

    // appsettings.json ships a "${JWT_SECRET_KEY}" placeholder that ASP.NET Core does NOT expand. Accepting it
    // (or any short key) would sign tokens with a publicly known secret, so refuse to start instead.
    if (secretKey.Contains("${", StringComparison.Ordinal) || Encoding.UTF8.GetByteCount(secretKey) < 32)
        throw new InvalidOperationException(
            "Jwt:SecretKey is a placeholder or shorter than 32 bytes. Set a strong random value " +
            "(e.g. `openssl rand -base64 64`) via appsettings.Development.json, user-secrets or the Jwt__SecretKey environment variable.");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"] ?? "VulscanApi",
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"] ?? "VulscanDashboard",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

    builder.Services.AddAuthorization();

    // Brute-force protection for the endpoints that accept credentials or tokens, per client IP. (Per-account lockout
    // is separate and lives in AuthService.) Behind a reverse proxy, enable Network:TrustForwardedHeaders so the
    // partition key is the real client address rather than the proxy's.
    var authPermits = builder.Configuration.GetValue("Security:AuthRateLimitPerMinute", 10);
    builder.Services.AddRateLimiter(limiter =>
    {
        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        limiter.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        limiter.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.Headers.RetryAfter = "60";
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(
                "{\"success\":false,\"message\":\"Too many attempts. Please wait a minute and try again.\"}", token);
        };
    });

    if (builder.Configuration.GetValue<bool>("Network:TrustForwardedHeaders"))
    {
        builder.Services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            var proxies = builder.Configuration.GetSection("Network:KnownProxies").Get<string[]>() ?? [];
            if (proxies.Length > 0)
            {
                o.KnownProxies.Clear();
                o.KnownNetworks.Clear();
                foreach (var p in proxies) o.KnownProxies.Add(System.Net.IPAddress.Parse(p));
            }
            else
            {
                // No explicit proxy list: trust whichever hop connects. Only safe when the API is reachable
                // solely through your proxy (firewall it), otherwise clients can spoof their IP.
                o.KnownNetworks.Clear();
                o.KnownProxies.Clear();
            }
        });
    }
    builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditingAuthorizationResultHandler>();

    // CORS — allow Angular dashboard origin
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowDashboard", policy =>
        {
            var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["https://localhost:4271"];

            policy.WithOrigins(origins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });

    // Swagger / OpenAPI
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Vulscan API",
            Version = "v1",
            Description = "Vulnerability scanning platform API for Azure DevOps on-premises environments."
        });

        // Use full type name to avoid schema ID conflicts
        options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token"
        });

        // Add the Bearer token requirement globally using the v2 API
        options.AddSecurityRequirement(doc =>
        {
            var schemeRef = new OpenApiSecuritySchemeReference("Bearer", doc);
            return new OpenApiSecurityRequirement { { schemeRef, [] } };
        });
    });

    var app = builder.Build();

    // ---------------------------------------------------------------------------
    // Middleware Pipeline
    // ---------------------------------------------------------------------------

    if (app.Configuration.GetValue<bool>("Network:TrustForwardedHeaders"))
        app.UseForwardedHeaders();

    // CORS must be before other middleware to handle preflight requests
    app.UseCors("AllowDashboard");

    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (!app.Environment.IsDevelopment())
        app.UseHsts();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Vulscan API v1");
            options.RoutePrefix = "swagger";
        });
    }

    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseMiddleware<PasswordChangeRequiredMiddleware>();
    app.UseAuthorization();
    app.MapControllers();

    // ---------------------------------------------------------------------------
    // Database initialisation: EF Core migrations (never EnsureCreated, which cannot evolve a schema).
    // Always applied in Development; elsewhere opt in with Database:MigrateOnStartup=true, or run
    // `dotnet ef database update` as a deployment step.
    // ---------------------------------------------------------------------------

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<VulscanDbContext>();
        var startupLog = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
            await DatabaseInitializer.MigrateAsync(db, startupLog);

        // Encrypt any secrets earlier versions stored in plaintext. Idempotent; a failure (e.g. schema not yet
        // migrated) must not stop the API, but is logged loudly.
        try
        {
            await SecretsMigrator.EncryptLegacySecretsAsync(
                db,
                scope.ServiceProvider.GetRequiredService<Vulscan.Application.Common.CredentialVault>(),
                scope.ServiceProvider.GetRequiredService<Vulscan.Application.Interfaces.ISecretProtector>(),
                startupLog);
        }
        catch (Exception ex)
        {
            startupLog.LogError(ex, "Could not encrypt legacy plaintext secrets; stored credentials may still be unencrypted.");
        }
    }

    Log.Information("Vulscan API started successfully on {Urls}", string.Join(", ", app.Urls));
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Vulscan API terminated unexpectedly");
    // A failed start must not look like a clean exit (supervisors, CI and debuggers key off the exit code).
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

// Lets integration tests host the real application (WebApplicationFactory<Program>).
public partial class Program;
