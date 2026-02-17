using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulscan.Domain.Entities;

namespace Vulscan.Infrastructure.Data.Configurations;

public class DiscoveredPackageConfiguration : IEntityTypeConfiguration<DiscoveredPackage>
{
    public void Configure(EntityTypeBuilder<DiscoveredPackage> builder)
    {
        builder.ToTable("DiscoveredPackages");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Ecosystem).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(500).IsRequired();
        builder.Property(p => p.Version).HasMaxLength(100).IsRequired();
        builder.Property(p => p.SourceFile).HasMaxLength(500).IsRequired();
        builder.Property(p => p.License).HasMaxLength(200);
        builder.Property(p => p.PackageUrl).HasMaxLength(1000);
        builder.Property(p => p.Purl).HasMaxLength(1000);

        // Indexes for common queries
        builder.HasIndex(p => new { p.ScanRunId, p.Ecosystem });
        builder.HasIndex(p => new { p.RepositoryId, p.Name });
        builder.HasIndex(p => p.HasVulnerabilities);

        // Relationships
        builder.HasOne(p => p.ScanRun)
            .WithMany()
            .HasForeignKey(p => p.ScanRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Repository)
            .WithMany()
            .HasForeignKey(p => p.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Sbom)
            .WithMany()
            .HasForeignKey(p => p.SbomId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
