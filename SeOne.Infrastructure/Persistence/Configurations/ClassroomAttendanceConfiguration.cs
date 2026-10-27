using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public sealed class ClassroomAttendanceConfiguration : IEntityTypeConfiguration<ClassroomAttendance>
{
    public void Configure(EntityTypeBuilder<ClassroomAttendance> builder)
    {
        builder.ToTable("ClassroomAttendances");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique();
        builder.HasOne(x => x.Session).WithMany(x => x.Attendance).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}
