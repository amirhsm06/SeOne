using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public sealed class ClassGroupMessageConfiguration : IEntityTypeConfiguration<ClassGroupMessage>
{
    public void Configure(EntityTypeBuilder<ClassGroupMessage> builder)
    {
        builder.ToTable("ClassGroupMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SenderRole).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Content).IsRequired().HasMaxLength(5000);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => new { x.GroupId, x.CreatedAt, x.Id });
        builder.HasIndex(x => x.SenderId);
        builder.HasOne<ClassGroup>()
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
