using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class LearningSessionConfiguration : IEntityTypeConfiguration<LearningSession>
{
    public void Configure(EntityTypeBuilder<LearningSession> builder)
    {
        builder.ToTable("LearningSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StartedAt).IsRequired();
        builder.Property(x => x.EndedAt).IsRequired(false);

        builder.HasIndex(x => new { x.StudentId, x.EndedAt });
        builder.HasIndex(x => new { x.StudentId, x.StartedAt });
        builder.HasIndex(x => x.CourseId);
        builder.HasIndex(x => x.LessonId);

        builder.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Enrollment).WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Lesson).WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
    }
}
