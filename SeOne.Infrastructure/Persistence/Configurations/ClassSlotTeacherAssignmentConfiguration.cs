using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public sealed class ClassSlotTeacherAssignmentConfiguration : IEntityTypeConfiguration<ClassSlotTeacherAssignment>
{
    public void Configure(EntityTypeBuilder<ClassSlotTeacherAssignment> builder)
    {
        builder.ToTable("ClassSlotTeacherAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => x.ClassSlotId).IsUnique();
        builder.HasIndex(x => new { x.TeacherId, x.Status });
        builder.HasOne(x => x.ClassSlot)
            .WithMany(x => x.TeacherAssignments)
            .HasForeignKey(x => x.ClassSlotId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Teacher)
            .WithMany()
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
