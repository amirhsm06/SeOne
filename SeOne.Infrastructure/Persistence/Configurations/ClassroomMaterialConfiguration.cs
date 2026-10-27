using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public sealed class ClassroomMaterialConfiguration : IEntityTypeConfiguration<ClassroomMaterial>
{
    public void Configure(EntityTypeBuilder<ClassroomMaterial> builder)
    {
        builder.ToTable("ClassroomMaterials");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(160).IsRequired();
        builder.Property(x => x.ObjectPath).HasMaxLength(512).IsRequired();
        builder.HasIndex(x => x.ObjectPath).IsUnique();
        builder.HasIndex(x => new { x.SessionId, x.CreatedAt });
        builder.HasOne(x => x.Session).WithMany(x => x.Materials).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);
    }
}
