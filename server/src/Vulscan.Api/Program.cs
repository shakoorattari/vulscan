using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;
using Vulscan.Api.Middleware;
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

    // ---------------------------------------------------------------------------
    // Service Registration
    // ---------------------------------------------------------------------------

    // Clean Architecture layers
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // HTTP context access for CurrentUserService
    builder.Services.AddHttpContextAccessor();

    // Controllers with JSON options
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

    // JWT Authentication
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var secretKey = jwtSection["SecretKey"]
        ?? throw new InvalidOperationException("JWT SecretKey must be configured in appsettings.");

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

    // CORS — allow Angular dashboard origin
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowDashboard", policy =>
        {
            var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:4200"];

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

    // CORS must be before other middleware to handle preflight requests
    app.UseCors("AllowDashboard");

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Vulscan API v1");
            options.RoutePrefix = "swagger";
        });
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // ---------------------------------------------------------------------------
    // Database Initialization (development only — use migrations in production)
    // ---------------------------------------------------------------------------

    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VulscanDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    Log.Information("Vulscan API started successfully on {Urls}", string.Join(", ", app.Urls));
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Vulscan API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
