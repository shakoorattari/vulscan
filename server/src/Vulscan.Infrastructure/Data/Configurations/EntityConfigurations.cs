using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulscan.Domain.Entities;

namespace Vulscan.Infrastructure.Data.Configurations;

public class AzureDevOpsInstanceConfiguration : IEntityTypeConfiguration<AzureDevOpsInstance>
{
    public void Configure(EntityTypeBuilder<AzureDevOpsInstance> builder)
    {
        builder.ToTable("AzureDevOpsInstances");

        builder.HasKey(i => i.Id);
        builder.HasIndex(i => i.Name).IsUnique();

        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Url).HasMaxLength(500).IsRequired();
        builder.Property(i => i.Collection).HasMaxLength(200).IsRequired();
        builder.Property(i => i.AuthMethod).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(i => i.CredentialReference).HasMaxLength(500);
        builder.Property(i => i.IsEnabled).HasDefaultValue(true);
    }
}

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(p => p.Id);
        builder.HasIndex(p => new { p.InstanceId, p.AzureProjectId }).IsUnique();

        builder.Property(p => p.Name).HasMaxLength(300).IsRequired();
        builder.Property(p => p.AzureProjectId).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Url).HasMaxLength(1000).IsRequired();
        builder.Property(p => p.CredentialReference).HasMaxLength(1000);
        builder.Property(p => p.DefaultBranch).HasMaxLength(200);
        builder.Property(p => p.IsEnabled).HasDefaultValue(true);
        builder.Property(p => p.CronExpression).HasMaxLength(100);

        builder.HasOne(p => p.Instance)
            .WithMany(i => i.Projects)
            .HasForeignKey(p => p.InstanceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RepositoryConfiguration : IEntityTypeConfiguration<Repository>
{
    public void Configure(EntityTypeBuilder<Repository> builder)
    {
        builder.ToTable("Repositories");

        builder.HasKey(r => r.Id);
        builder.HasIndex(r => new { r.ProjectId, r.Name }).IsUnique();

        builder.Property(r => r.Name).HasMaxLength(300).IsRequired();
        builder.Property(r => r.CloneUrl).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.DefaultBranch).HasMaxLength(200).HasDefaultValue("main");
        builder.Property(r => r.LastScannedCommit).HasMaxLength(100);
        builder.Property(r => r.IsEnabled).HasDefaultValue(true);

        builder.HasOne(r => r.Project)
            .WithMany(p => p.Repositories)
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
