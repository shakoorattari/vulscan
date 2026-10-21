using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Vulscan.Infrastructure.Data;

/// <summary>
/// Brings the database schema up to date using EF Core migrations.
/// <para>
/// Databases created by <c>EnsureCreated</c> (the previous development behaviour) have the tables but no
/// migration history, so <c>Migrate</c> would try to create them again and fail. Those databases are
/// baselined once: the migrations that the schema already reflects are recorded as applied, then normal
/// migration continues from there.
/// </para>
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Last migration whose schema matches what <c>EnsureCreated</c> produced. Never advance this: it
    /// describes databases created before migrations were adopted, not databases created later.
    /// </summary>
    public const string BaselineMigrationId = "20260501104726_AddEmailNotificationSystem";

    private static readonly string[] BaselineMarkerTables =
        ["Users", "AzureDevOpsInstances", "Projects", "Repositories", "ScanRuns", "Vulnerabilities",
         "AuditLogs", "ScheduleSettings", "RepositoryBranches", "SmtpConfigurations", "EmailLogs"];

    public static async Task MigrateAsync(VulscanDbContext db, ILogger logger, CancellationToken ct = default)
    {
        if (!db.Database.IsSqlite() && await NeedsBaselineAsync(db, ct))
            await BaselineAsync(db, logger, ct);

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count > 0)
            logger.LogInformation("Applying {Count} database migration(s): {Migrations}", pending.Count, string.Join(", ", pending));

        await db.Database.MigrateAsync(ct);
    }

    private static async Task<bool> NeedsBaselineAsync(VulscanDbContext db, CancellationToken ct)
    {
        var creator = db.GetService<IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync(ct)) return false;            // fresh server: Migrate will create everything

        var applied = await db.Database.GetAppliedMigrationsAsync(ct);
        if (applied.Any()) return false;                              // already under migration control

        return await creator.HasTablesAsync(ct);                      // tables but no history => EnsureCreated database
    }

    private static async Task BaselineAsync(VulscanDbContext db, ILogger logger, CancellationToken ct)
    {
        var existing = await ExistingTablesAsync(db, ct);
        var missing = BaselineMarkerTables.Where(t => !existing.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0)
        {
            // Refuse to guess: recording migrations as applied when the schema does not match would hide the problem.
            throw new InvalidOperationException(
                "The database has tables but no migration history, and does not match the expected baseline schema " +
                $"(missing: {string.Join(", ", missing)}). Migrate it manually or recreate it.");
        }

        var history = db.GetService<IHistoryRepository>();
        await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), ct);

        var baseline = db.Database.GetMigrations()
            .Where(m => string.CompareOrdinal(m, BaselineMigrationId) <= 0)
            .ToList();

        foreach (var id in baseline)
        {
#pragma warning disable EF1001 // ProductInfo is the supported way to stamp the EF version on a history row.
            var row = new HistoryRow(id, ProductInfo.GetVersion());
#pragma warning restore EF1001
            await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(row), ct);
        }

        logger.LogWarning(
            "Existing database had no migration history; recorded {Count} migration(s) up to {Baseline} as already applied.",
            baseline.Count, BaselineMigrationId);
    }

    private static async Task<List<string>> ExistingTablesAsync(VulscanDbContext db, CancellationToken ct) =>
        await db.Database
            .SqlQueryRaw<string>("SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'vulscan'")
            .ToListAsync(ct);
}
