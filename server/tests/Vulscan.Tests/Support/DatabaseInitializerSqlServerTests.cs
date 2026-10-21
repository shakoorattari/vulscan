using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Vulscan.Infrastructure.Data;

namespace Vulscan.Tests.Support;

/// <summary>Run with VULSCAN_TEST_SQL set. Each test uses its own scratch database.</summary>
public class DatabaseInitializerSqlServerTests
{

    [SqlServerFact]
    public async Task Fresh_server_gets_the_full_schema_from_migrations()
    {
        await using var scratch = new ScratchSqlDatabase();
        await using (var db = scratch.NewContext())
            await DatabaseInitializer.MigrateAsync(db, NullLogger.Instance);

        await using var verify = scratch.NewContext();
        Assert.Empty(await verify.Database.GetPendingMigrationsAsync());
        Assert.Contains("Username", await scratch.ScalarsAsync(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='vulscan' AND TABLE_NAME='AuditLogs'"));
        Assert.Single(await scratch.ScalarsAsync("SELECT Username FROM vulscan.Users WHERE Username = 'admin'"));
    }

    [SqlServerFact]
    public async Task EnsureCreated_style_database_is_baselined_then_brought_up_to_date_without_losing_data()
    {
        await using var scratch = new ScratchSqlDatabase();

        // Reproduce the legacy state: the schema as of the baseline migration, but no migration history.
        await using (var db = scratch.NewContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(DatabaseInitializer.BaselineMigrationId);
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO vulscan.AuditLogs (Id, CreatedAt, Action, EntityType, Timestamp) VALUES (NEWID(), SYSUTCDATETIME(), N'Legacy.Event', N'Legacy', SYSUTCDATETIME())");
            await db.Database.ExecuteSqlRawAsync("DROP TABLE dbo.__EFMigrationsHistory");
        }
        Assert.DoesNotContain("Username", await scratch.ScalarsAsync(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='vulscan' AND TABLE_NAME='AuditLogs'"));

        await using (var db = scratch.NewContext())
            await DatabaseInitializer.MigrateAsync(db, NullLogger.Instance);

        await using var verify = scratch.NewContext();
        var all = verify.Database.GetMigrations().ToList();
        var applied = (await verify.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.Equal(all, applied);                                       // baselined + newer ones applied
        Assert.True(all.Count > 4, "expected at least one migration newer than the baseline");

        Assert.Contains("Username", await scratch.ScalarsAsync(           // newer migration really ran
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='vulscan' AND TABLE_NAME='AuditLogs'"));
        Assert.Equal("Legacy.Event", Assert.Single(await scratch.ScalarsAsync("SELECT Action FROM vulscan.AuditLogs")));   // data kept
        Assert.Equal("Success", Assert.Single(await scratch.ScalarsAsync("SELECT Outcome FROM vulscan.AuditLogs")));       // default applied
    }

    [SqlServerFact]
    public async Task Running_twice_is_a_no_op()
    {
        await using var scratch = new ScratchSqlDatabase();
        await using (var db = scratch.NewContext()) await DatabaseInitializer.MigrateAsync(db, NullLogger.Instance);

        var before = await scratch.ScalarsAsync("SELECT COUNT(*) FROM dbo.__EFMigrationsHistory");
        await using (var db = scratch.NewContext()) await DatabaseInitializer.MigrateAsync(db, NullLogger.Instance);

        Assert.Equal(before, await scratch.ScalarsAsync("SELECT COUNT(*) FROM dbo.__EFMigrationsHistory"));
    }

    [SqlServerFact]
    public async Task Refuses_to_baseline_a_schema_it_does_not_recognise()
    {
        await using var scratch = new ScratchSqlDatabase();
        await using (var db = scratch.NewContext())
        {
            await db.Database.EnsureCreatedAsync();                       // creates the DB + all tables
            await db.Database.ExecuteSqlRawAsync("DROP TABLE vulscan.EmailLogs");        // now it is neither baseline nor empty
        }

        await using var again = scratch.NewContext();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseInitializer.MigrateAsync(again, NullLogger.Instance));
        Assert.Contains("EmailLogs", ex.Message);
    }
}
