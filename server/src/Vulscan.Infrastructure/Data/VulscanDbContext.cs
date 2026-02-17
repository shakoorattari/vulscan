using Microsoft.EntityFrameworkCore;
using Vulscan.Domain.Entities;

namespace Vulscan.Infrastructure.Data;

/// <summary>
/// Primary EF Core database context for the Vulscan platform.
/// </summary>
public class VulscanDbContext(DbContextOptions<VulscanDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AzureDevOpsInstance> AzureDevOpsInstances => Set<AzureDevOpsInstance>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<ScanRun> ScanRuns => Set<ScanRun>();
    public DbSet<Sbom> Sboms => Set<Sbom>();
    public DbSet<DiscoveredPackage> DiscoveredPackages => Set<DiscoveredPackage>();
    public DbSet<Vulnerability> Vulnerabilities => Set<Vulnerability>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SQLite doesn't support schemas
        if (!Database.IsSqlite())
        {
            modelBuilder.HasDefaultSchema("vulscan");
        }

        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VulscanDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
