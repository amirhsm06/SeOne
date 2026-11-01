using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public sealed class ClassEnrollmentConfiguration : IEntityTypeConfiguration<ClassEnrollment>
{
    public void Configure(EntityTypeBuilder<ClassEnrollment> builder)
    {
        builder.ToTable("ClassEnrollments");
        builder.HasKey(enrollment => enrollment.Id);
        builder.Property(enrollment => enrollment.FinalPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(enrollment => enrollment.DiscountPercent).HasPrecision(5, 2).IsRequired();
        builder.Property(enrollment => enrollment.Status).HasMaxLength(20).IsRequired();
        builder.Property(enrollment => enrollment.InviteCode).HasMaxLength(16);
        builder.Property(enrollment => enrollment.EnrolledAt).IsRequired();
        builder.HasIndex(enrollment => new { enrollment.ClassId, enrollment.StudentId }).IsUnique();
        builder.HasIndex(enrollment => new { enrollment.ClassId, enrollment.Status });
        builder.HasOne(enrollment => enrollment.CourseClass).WithMany(classItem => classItem.Enrollments).HasForeignKey(enrollment => enrollment.ClassId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(enrollment => enrollment.Student).WithMany().HasForeignKey(enrollment => enrollment.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(enrollment => enrollment.InvitedByStudent).WithMany().HasForeignKey(enrollment => enrollment.InvitedByStudentId).OnDelete(DeleteBehavior.Restrict);
    }
}
