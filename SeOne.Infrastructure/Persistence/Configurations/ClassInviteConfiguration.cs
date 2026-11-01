using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public sealed class ClassInviteConfiguration : IEntityTypeConfiguration<ClassInvite>
{
    public void Configure(EntityTypeBuilder<ClassInvite> builder)
    {
        builder.ToTable("ClassInvites");
        builder.HasKey(invite => invite.Id);
        builder.Property(invite => invite.Code).HasMaxLength(16).IsRequired();
        builder.Property(invite => invite.CreatedAt).IsRequired();
        builder.HasIndex(invite => invite.Code).IsUnique();
        builder.HasIndex(invite => new { invite.ClassId, invite.CreatedAt });
        builder.HasOne(invite => invite.CourseClass).WithMany(classItem => classItem.Invites).HasForeignKey(invite => invite.ClassId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(invite => invite.CreatedBy).WithMany().HasForeignKey(invite => invite.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
