using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vulscan.Application.Interfaces;
using Vulscan.Infrastructure.BackgroundServices;
using Vulscan.Infrastructure.Clients;
using Vulscan.Infrastructure.Data;
using Vulscan.Infrastructure.Services;

namespace Vulscan.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("VulscanDb");
        var useSqlite = configuration.GetValue<bool>("UseSqlite", false);

        // Entity Framework Core — SQLite for dev or SQL Server for prod
        services.AddDbContext<VulscanDbContext>(options =>
        {
            if (useSqlite)
            {
                options.UseSqlite(
                    connectionString,
                    sqliteOptions => sqliteOptions.MigrationsAssembly(typeof(VulscanDbContext).Assembly.FullName));
            }
            else
            {
                options.UseSqlServer(
                    connectionString,
                    sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(typeof(VulscanDbContext).Assembly.FullName);
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorNumbersToAdd: null);
                        sqlOptions.CommandTimeout(60);
                    });
            }
        });

        // Register VulscanDbContext as DbContext for Application layer services
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<VulscanDbContext>());

        // Infrastructure services
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ISmtpConfigurationService, SmtpConfigurationService>();
        services.AddScoped<IEmailService, EmailService>();

        // Azure DevOps client (HTTP client with longer timeout for repo operations)
        services.AddHttpClient<IAzureDevOpsClient, AzureDevOpsClient>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        // ── Vulnerability scanning (OSV + cache) ──────────────────────────────
        services.Configure<VulnerabilityCacheOptions>(
            configuration.GetSection(VulnerabilityCacheOptions.SectionName));

        // OSV.dev API client (https://osv.dev) — free, no API key, ecosystem-aware
        services.AddHttpClient<IOsvApiClient, OsvApiClient>((sp, client) =>
        {
            var baseUrl = configuration["VulnerabilityScanning:OSV:ApiUrl"] ?? "https://api.osv.dev/";
            if (!baseUrl.EndsWith('/')) baseUrl += "/";
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("User-Agent", "Vulscan/1.0");
            client.Timeout = TimeSpan.FromSeconds(
                configuration.GetValue("VulnerabilityScanning:OSV:TimeoutSeconds", 30));
        });

        // Memory cache for OSV lookups (Phase 2)
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = configuration.GetValue<long?>(
                "VulnerabilityScanning:Cache:SizeLimit") ?? 100_000;
        });
        services.AddSingleton<IVulnerabilityCacheService, VulnerabilityCacheService>();

        // Dependency scanner for vulnerability detection
        services.AddScoped<IDependencyScanner, DependencyScanner>();

        // Background worker for processing scans
        services.AddHostedService<ScanBackgroundWorker>();

        // Cron-based scheduler that enqueues scans (global + per-project overrides)
        services.AddHostedService<ProjectScanScheduler>();

        return services;
    }
}
