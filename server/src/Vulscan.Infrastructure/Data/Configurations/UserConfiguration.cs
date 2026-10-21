using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulscan.Domain.Entities;

namespace Vulscan.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    // Pre-computed BCrypt hash for default admin password with workfactor 12
    // DEFAULT PASSWORD (DEVELOPMENT ONLY): Vulscan@2025
    // ⚠️ SECURITY: Change this password immediately after deployment!
    // This is static to avoid EF Core migration warnings about dynamic values
    private const string AdminPasswordHash = "$2a$12$6iQL3yNjcPI40mMYzoinqOMnbCf6sIwyu2iQczG7DCK19bEAezyp.";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.HasIndex(u => u.Username).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Username).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(512);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(u => u.IsActive).HasDefaultValue(true);

        builder.Property(u => u.AuthProvider).HasConversion<string>().HasMaxLength(20).IsRequired().HasDefaultValue(Domain.Enums.AuthProvider.Local);
        builder.Property(u => u.ExternalId).HasMaxLength(100);
        // An external identity maps to at most one user; local users (no ExternalId) are unconstrained.
        builder.HasIndex(u => u.ExternalId).IsUnique().HasFilter("[ExternalId] IS NOT NULL");

        builder.Property(u => u.LastAccessReviewBy).HasMaxLength(256);
        builder.Property(u => u.DeactivationReason).HasMaxLength(500);

        // Seed default admin user (password should be changed immediately after deployment)
        builder.HasData(new User
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Username = "admin",
            Email = "admin@vulscan.local",
            PasswordHash = AdminPasswordHash,
            Role = Domain.Enums.UserRole.Admin,
            IsActive = true,
            // The seeded password is public (README). It must be replaced at first sign-in.
            MustChangePassword = true,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
