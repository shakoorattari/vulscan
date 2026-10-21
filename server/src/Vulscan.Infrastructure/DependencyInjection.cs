using System.Security.Cryptography.X509Certificates;
using Vulscan.Application.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulscan.Application.Interfaces;
using Vulscan.Infrastructure.BackgroundServices;
using Vulscan.Infrastructure.Clients;
using Vulscan.Infrastructure.Data;
using Vulscan.Infrastructure.Security;
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
        services.AddHttpContextAccessor();
        services.AddSingleton<IAuditService, AuditService>();
        AddSecretProtection(services, configuration);

        // Authentication & access control
        services.AddSingleton(configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions());
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IEntraTokenValidator, EntraTokenValidator>();
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

        // Background jobs (scan worker + cron scheduler). Disable with BackgroundJobs:Enabled=false on instances that
        // should only serve the API (e.g. a second instance for diagnostics, or API replicas split from workers) —
        // otherwise every instance polls the same queue.
        if (configuration.GetValue("BackgroundJobs:Enabled", true))
        {
            // Background worker for processing scans
            services.AddHostedService<ScanBackgroundWorker>();

            // Cron-based scheduler that enqueues scans (global + per-project overrides)
            services.AddHostedService<ProjectScanScheduler>();

            // Removal of unused access
            services.AddHostedService<DormantAccountSweeper>();
        }

        return services;
    }

    /// <summary>
    /// Registers encryption-at-rest for stored secrets. The key ring is persisted outside the database
    /// (a database backup alone must not be enough to decrypt the credentials inside it).
    /// <list type="bullet">
    /// <item><c>DataProtection:KeysPath</c> — key ring directory (default: <c>~/.vulscan/keys</c>). Mount a persistent, backed-up volume in containers.</item>
    /// <item><c>DataProtection:CertificatePath</c> / <c>CertificatePassword</c> — optional X.509 certificate that encrypts the key ring at rest (recommended in production).</item>
    /// </list>
    /// </summary>
    private static void AddSecretProtection(IServiceCollection services, IConfiguration configuration)
    {
        var keysPath = configuration["DataProtection:KeysPath"];
        if (string.IsNullOrWhiteSpace(keysPath))
            keysPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vulscan", "keys");

        Directory.CreateDirectory(keysPath);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(keysPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var builder = services.AddDataProtection()
            .SetApplicationName("Vulscan")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        var certPath = configuration["DataProtection:CertificatePath"];
        if (!string.IsNullOrWhiteSpace(certPath))
        {
            builder.ProtectKeysWithCertificate(
                X509CertificateLoader.LoadPkcs12FromFile(certPath, configuration["DataProtection:CertificatePassword"]));
        }

        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
    }
}
