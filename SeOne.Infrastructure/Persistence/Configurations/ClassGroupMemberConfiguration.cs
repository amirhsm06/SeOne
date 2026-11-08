using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public sealed class ClassGroupMemberConfiguration : IEntityTypeConfiguration<ClassGroupMember>
{
    public void Configure(EntityTypeBuilder<ClassGroupMember> builder)
    {
        builder.ToTable("ClassGroupMembers");
        builder.HasKey(x => new { x.GroupId, x.UserId });
        builder.Property(x => x.Role).IsRequired().HasMaxLength(30);
        builder.Property(x => x.JoinedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => new { x.UserId, x.IsActive });
        builder.HasOne<ClassGroup>()
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
