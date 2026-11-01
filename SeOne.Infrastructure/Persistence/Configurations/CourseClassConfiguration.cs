using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public sealed class CourseClassConfiguration : IEntityTypeConfiguration<CourseClass>
{
    public void Configure(EntityTypeBuilder<CourseClass> builder)
    {
        builder.ToTable("CourseClasses");
        builder.HasKey(classItem => classItem.Id);
        builder.Property(classItem => classItem.Title).HasMaxLength(200).IsRequired();
        builder.Property(classItem => classItem.Description).HasMaxLength(4000).IsRequired();
        builder.Property(classItem => classItem.Level).HasMaxLength(50).IsRequired();
        builder.Property(classItem => classItem.Subject).HasMaxLength(100).IsRequired();
        builder.Property(classItem => classItem.Type).HasMaxLength(20).IsRequired();
        builder.Property(classItem => classItem.Status).HasMaxLength(20).IsRequired();
        builder.Property(classItem => classItem.MaxCapacity).IsRequired();
        builder.Property(classItem => classItem.BasePrice).HasPrecision(18, 2).IsRequired();
        builder.Property(classItem => classItem.ScheduledDate).HasColumnType("date").IsRequired();
        builder.Property(classItem => classItem.StartTime).HasColumnType("time").IsRequired();
        builder.Property(classItem => classItem.EndTime).HasColumnType("time").IsRequired();
        builder.Property(classItem => classItem.TimeZone).HasMaxLength(100).IsRequired();
        builder.Property(classItem => classItem.MeetingUrl).HasMaxLength(2048);
        builder.Property(classItem => classItem.CreatedAt).IsRequired();
        builder.HasIndex(classItem => new { classItem.TeacherId, classItem.ScheduledDate, classItem.Status });
        builder.HasIndex(classItem => new { classItem.Status, classItem.ScheduledDate });
        builder.HasOne(classItem => classItem.Course).WithMany().HasForeignKey(classItem => classItem.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(classItem => classItem.Teacher).WithMany().HasForeignKey(classItem => classItem.TeacherId).OnDelete(DeleteBehavior.Restrict);
    }
}
