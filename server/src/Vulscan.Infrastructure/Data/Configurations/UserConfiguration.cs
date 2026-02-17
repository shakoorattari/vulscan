using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulscan.Domain.Entities;

namespace Vulscan.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    // Pre-computed BCrypt hash for "Admin@123!" with workfactor 12
    // This is static to avoid EF Core migration warnings about dynamic values
    private const string AdminPasswordHash = "$2a$12$OOF3yNNPWG8p2JnOjF4V5u83Oc..5jM7H5vvgNd7cJc5PQG.DLhVW";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.HasIndex(u => u.Username).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Username).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(u => u.IsActive).HasDefaultValue(true);

        // Seed default admin user (password: Admin@123!)
        builder.HasData(new User
        {
            Id = 1,
            Username = "admin",
            Email = "admin@vulscan.local",
            PasswordHash = AdminPasswordHash,
            Role = Domain.Enums.UserRole.Admin,
            IsActive = true,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
