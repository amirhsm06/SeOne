using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public sealed class TeacherGroupChatMessageConfiguration : IEntityTypeConfiguration<TeacherGroupChatMessage>
{
    public void Configure(EntityTypeBuilder<TeacherGroupChatMessage> builder)
    {
        builder.ToTable("TeacherGroupChatMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.SenderRole).HasMaxLength(30).IsRequired();
        builder.Property(message => message.Content).HasMaxLength(5000).IsRequired();
        builder.Property(message => message.IsAnnouncement).IsRequired();
        builder.Property(message => message.CreatedAt).IsRequired();
        builder.HasIndex(message => message.SenderId);
        builder.HasIndex(message => new { message.CreatedAt, message.Id });
    }
}
