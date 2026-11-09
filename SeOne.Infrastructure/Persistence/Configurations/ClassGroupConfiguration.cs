using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public sealed class ClassGroupConfiguration : IEntityTypeConfiguration<ClassGroup>
{
    public void Configure(EntityTypeBuilder<ClassGroup> builder)
    {
        builder.ToTable("ClassGroups");
        builder.HasKey(group => group.Id);
        builder.Property(group => group.Title).HasMaxLength(180).IsRequired();
        builder.Property(group => group.GroupType).HasMaxLength(20).IsRequired();
        builder.Property(group => group.CreatedAt).IsRequired();
        builder.Property(group => group.IsActive).IsRequired();
        builder.HasIndex(group => group.ClassId).IsUnique();
        builder.HasIndex(group => group.IsActive);
        builder.HasOne<CourseClass>().WithMany().HasForeignKey(group => group.ClassId).OnDelete(DeleteBehavior.Cascade);
    }
}
