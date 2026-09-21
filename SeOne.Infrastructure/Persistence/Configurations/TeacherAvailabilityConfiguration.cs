using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class TeacherAvailabilityConfiguration : IEntityTypeConfiguration<TeacherAvailability>
{
    public void Configure(EntityTypeBuilder<TeacherAvailability> builder)
    {
        builder.ToTable("TeacherAvailabilities");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TeacherId).IsRequired();

        builder.Property(t => t.DayOfWeek).IsRequired();

        builder.Property(t => t.StartTime).IsRequired().HasColumnType("time");

        builder.Property(t => t.EndTime).IsRequired().HasColumnType("time");

        builder.Property(t => t.IsAvailable).IsRequired();

        builder.Property(t => t.CreatedAt).IsRequired();

        builder.HasIndex(t => t.TeacherId);

        builder.HasIndex(t => new { t.TeacherId, t.DayOfWeek, t.StartTime, t.EndTime }).IsUnique();

        builder.HasOne(t => t.Teacher)
            .WithMany()
            .HasForeignKey(t => t.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
