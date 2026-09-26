using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("Lessons");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.CourseModuleId).IsRequired();

        builder.Property(l => l.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.Content)
            .IsRequired()
            .HasMaxLength(20000);

        builder.Property(l => l.Description)
            .HasMaxLength(2000);

        builder.Property(l => l.VideoUrl)
            .HasMaxLength(500);

        builder.Property(l => l.AudioUrl)
            .HasMaxLength(500);

        builder.Property(l => l.Duration)
            .IsRequired(false);

        builder.Property(l => l.Order).IsRequired();

        builder.Property(l => l.CreatedAt).IsRequired();

        builder.HasIndex(l => l.CourseModuleId);

        builder.HasIndex(l => new { l.CourseModuleId, l.Order }).IsUnique();

        builder.HasOne(l => l.CourseModule)
            .WithMany()
            .HasForeignKey(l => l.CourseModuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
