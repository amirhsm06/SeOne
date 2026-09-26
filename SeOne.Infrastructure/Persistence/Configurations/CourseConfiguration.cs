using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Level)
            .HasMaxLength(50);

        builder.Property(x => x.Price)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("IRR");

        builder.Property(x => x.DiscountPercent)
            .HasPrecision(5, 2)
            .HasDefaultValue(0m);

        builder.Property(x => x.Duration)
            .HasMaxLength(100);

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(500);

        builder.Property(x => x.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Language)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.IsFeatured)
            .IsRequired();

        builder.Property(x => x.IsPublished)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.Language);
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.Level);
        builder.HasIndex(x => x.IsFeatured);

        builder.HasOne(x => x.Teacher)
            .WithMany()
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
