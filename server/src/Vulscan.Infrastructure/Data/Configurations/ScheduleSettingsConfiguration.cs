using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulscan.Domain.Entities;

namespace Vulscan.Infrastructure.Data.Configurations;

public class ScheduleSettingsConfiguration : IEntityTypeConfiguration<ScheduleSettings>
{
    public void Configure(EntityTypeBuilder<ScheduleSettings> builder)
    {
        builder.ToTable("ScheduleSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.CronExpression).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Enabled).HasDefaultValue(true);

        // Seed the singleton row
        builder.HasData(new ScheduleSettings
        {
            Id = ScheduleSettings.SingletonId,
            CronExpression = "0 2 * * *",
            Enabled = true,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
    }
}
