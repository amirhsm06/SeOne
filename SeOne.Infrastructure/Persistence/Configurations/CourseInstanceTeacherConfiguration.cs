using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class CourseInstanceTeacherConfiguration : IEntityTypeConfiguration<CourseInstanceTeacher>
{
    public void Configure(EntityTypeBuilder<CourseInstanceTeacher> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.CourseInstance)
            .WithMany(x => x.Teachers)
            .HasForeignKey(x => x.CourseInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Teacher)
            .WithMany()
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new
        {
            x.CourseInstanceId,
            x.TeacherId
        })
        .IsUnique();

        builder.HasIndex(x => x.TeacherId);
    }
}