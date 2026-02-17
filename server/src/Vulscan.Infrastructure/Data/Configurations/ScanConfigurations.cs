using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulscan.Domain.Entities;

namespace Vulscan.Infrastructure.Data.Configurations;

public class ScanRunConfiguration : IEntityTypeConfiguration<ScanRun>
{
    public void Configure(EntityTypeBuilder<ScanRun> builder)
    {
        builder.ToTable("ScanRuns");

        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.Status);
        builder.HasIndex(s => s.StartedAt);

        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        // ErrorLog uses default string mapping (TEXT for SQLite, nvarchar(max) for SQL Server)

        builder.HasOne(s => s.Instance)
            .WithMany(i => i.ScanRuns)
            .HasForeignKey(s => s.InstanceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.TriggeredBy)
            .WithMany(u => u.TriggeredScans)
            .HasForeignKey(s => s.TriggeredByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class SbomConfiguration : IEntityTypeConfiguration<Sbom>
{
    public void Configure(EntityTypeBuilder<Sbom> builder)
    {
        builder.ToTable("Sboms");

        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.RepositoryId, s.ScanRunId });

        builder.Property(s => s.Format).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Generator).HasMaxLength(50).IsRequired();
        // SbomJson uses default string mapping
        builder.Property(s => s.CommitHash).HasMaxLength(100);

        builder.HasOne(s => s.Repository)
            .WithMany(r => r.Sboms)
            .HasForeignKey(s => s.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.ScanRun)
            .WithMany(sr => sr.Sboms)
            .HasForeignKey(s => s.ScanRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VulnerabilityConfiguration : IEntityTypeConfiguration<Vulnerability>
{
    public void Configure(EntityTypeBuilder<Vulnerability> builder)
    {
        builder.ToTable("Vulnerabilities");

        builder.HasKey(v => v.Id);
        builder.HasIndex(v => v.CveId);
        builder.HasIndex(v => v.Severity);
        builder.HasIndex(v => v.Status);
        builder.HasIndex(v => v.PackageName);
        builder.HasIndex(v => new { v.RepositoryId, v.CveId, v.PackageName });

        builder.Property(v => v.CveId).HasMaxLength(50).IsRequired();
        builder.Property(v => v.PackageName).HasMaxLength(500).IsRequired();
        builder.Property(v => v.InstalledVersion).HasMaxLength(100).IsRequired();
        builder.Property(v => v.FixedVersion).HasMaxLength(100);
        builder.Property(v => v.Severity).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(v => v.CvssScore).HasPrecision(4, 2);
        builder.Property(v => v.CvssVector).HasMaxLength(200);
        // Description uses default string mapping
        builder.Property(v => v.SourceDb).HasMaxLength(100);
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.HasOne(v => v.Sbom)
            .WithMany(s => s.Vulnerabilities)
            .HasForeignKey(v => v.SbomId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(v => v.ScanRun)
            .WithMany(sr => sr.Vulnerabilities)
            .HasForeignKey(v => v.ScanRunId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(v => v.Repository)
            .WithMany(r => r.Vulnerabilities)
            .HasForeignKey(v => v.RepositoryId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => a.UserId);

        builder.Property(a => a.Action).HasMaxLength(200).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        // Details uses default string mapping
        builder.Property(a => a.IpAddress).HasMaxLength(50);

        builder.HasOne(a => a.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
