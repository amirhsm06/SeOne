using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class PracticeConfiguration : IEntityTypeConfiguration<Practice>
{
    public void Configure(EntityTypeBuilder<Practice> builder)
    {
        builder.ToTable("Practices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CourseId)
            .IsRequired();

        builder.Property(x => x.CourseModuleId)
            .IsRequired(false);

        builder.Property(x => x.LessonId)
            .IsRequired(false);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(5000);

        builder.Property(x => x.Language)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Level)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.TimeLimit)
            .IsRequired(false);

        builder.Property(x => x.PassingScore)
            .IsRequired();

        builder.Property(x => x.IsPublished)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => x.CourseId);
        builder.HasIndex(x => x.CourseModuleId);
        builder.HasIndex(x => x.LessonId);
        builder.HasIndex(x => new { x.CourseId, x.IsPublished });

        builder.HasOne(x => x.Course)
            .WithMany()
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CourseModule)
            .WithMany()
            .HasForeignKey(x => x.CourseModuleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Lesson)
            .WithMany()
            .HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
