using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public sealed class TeacherGroupChatMessageConfiguration : IEntityTypeConfiguration<TeacherGroupChatMessage>
{
    public void Configure(EntityTypeBuilder<TeacherGroupChatMessage> builder)
    {
        builder.ToTable("TeacherGroupChatMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SenderRole).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Content).IsRequired().HasMaxLength(5000);
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => new { x.CreatedAt, x.Id });
        builder.HasIndex(x => x.SenderId);
    }
}
