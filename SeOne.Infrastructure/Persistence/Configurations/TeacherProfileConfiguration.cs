using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class TeacherProfileConfiguration : IEntityTypeConfiguration<TeacherProfile>
{
    public void Configure(EntityTypeBuilder<TeacherProfile> builder)
    {
        builder.ToTable("TeacherProfiles");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TeacherId).IsRequired();

        builder.Property(t => t.Avatar).HasMaxLength(500);
        builder.Property(t => t.TeachingLanguage).HasMaxLength(100);
        builder.Property(t => t.Subject).HasMaxLength(100);
        builder.Property(t => t.Level).HasMaxLength(50);
        builder.Property(t => t.Bio).HasMaxLength(2000);

        builder.Property(t => t.Rating)
            .HasColumnType("decimal(3,2)")
            .HasDefaultValue(0m);

        builder.HasIndex(t => t.TeacherId).IsUnique();

        builder.HasOne(t => t.Teacher)
            .WithOne()
            .HasForeignKey<TeacherProfile>(t => t.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
