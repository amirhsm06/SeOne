using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public sealed class ClassroomSessionConfiguration : IEntityTypeConfiguration<ClassroomSession>
{
    public void Configure(EntityTypeBuilder<ClassroomSession> builder)
    {
        builder.ToTable("ClassroomSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(24).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(80).IsRequired();
        builder.Property(x => x.MeetingUrl).HasMaxLength(2048);
        builder.Property(x => x.RecordingUrl).HasMaxLength(2048);
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.HasIndex(x => new { x.TeacherId, x.ScheduledAt });
        builder.HasIndex(x => new { x.CourseId, x.ScheduledAt });
        builder.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
    }
}
