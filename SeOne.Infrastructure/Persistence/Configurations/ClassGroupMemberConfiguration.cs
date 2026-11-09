using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public sealed class ClassGroupMemberConfiguration : IEntityTypeConfiguration<ClassGroupMember>
{
    public void Configure(EntityTypeBuilder<ClassGroupMember> builder)
    {
        builder.ToTable("ClassGroupMembers");
        builder.HasKey(member => new { member.GroupId, member.UserId });
        builder.Property(member => member.Role).HasMaxLength(30).IsRequired();
        builder.Property(member => member.JoinedAt).IsRequired();
        builder.Property(member => member.IsActive).IsRequired();
        builder.HasIndex(member => new { member.UserId, member.IsActive });
        builder.HasOne<ClassGroup>().WithMany().HasForeignKey(member => member.GroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
