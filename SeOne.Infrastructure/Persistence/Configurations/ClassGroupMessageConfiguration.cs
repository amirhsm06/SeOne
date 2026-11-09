using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public sealed class ClassGroupMessageConfiguration : IEntityTypeConfiguration<ClassGroupMessage>
{
    public void Configure(EntityTypeBuilder<ClassGroupMessage> builder)
    {
        builder.ToTable("ClassGroupMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.SenderRole).HasMaxLength(30).IsRequired();
        builder.Property(message => message.Content).HasMaxLength(5000).IsRequired();
        builder.Property(message => message.CreatedAt).IsRequired();
        builder.HasIndex(message => message.SenderId);
        builder.HasIndex(message => new { message.GroupId, message.CreatedAt, message.Id });
        builder.HasOne<ClassGroup>().WithMany().HasForeignKey(message => message.GroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
