using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class LessonProgressConfiguration : IEntityTypeConfiguration<LessonProgress>
{
    public void Configure(EntityTypeBuilder<LessonProgress> builder)
    {
        builder.ToTable("LessonProgress");

        builder.HasKey(lp => lp.Id);

        builder.Property(lp => lp.StudentId).IsRequired();

        builder.Property(lp => lp.LessonId).IsRequired();

        builder.Property(lp => lp.IsCompleted).IsRequired();

        builder.Property(lp => lp.CompletedAt).IsRequired(false);

        builder.Property(lp => lp.TimeSpent)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(lp => lp.LastPosition)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(lp => lp.LastAccessedAt).IsRequired(false);

        builder.HasIndex(lp => new { lp.StudentId, lp.LessonId }).IsUnique();

        builder.HasIndex(lp => lp.LessonId);

        builder.HasOne(lp => lp.Student)
            .WithMany()
            .HasForeignKey(lp => lp.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(lp => lp.Lesson)
            .WithMany()
            .HasForeignKey(lp => lp.LessonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
