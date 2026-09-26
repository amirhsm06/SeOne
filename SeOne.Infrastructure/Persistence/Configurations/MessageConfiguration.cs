using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class MessageConfiguration
    : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ConversationId)
            .IsRequired();

        builder.Property(x => x.SenderId)
            .IsRequired();

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(x => x.Attachments)
            .HasConversion(
                new ValueConverter<List<string>, string>(
                    value => JsonSerializer.Serialize(
                        value ?? new List<string>(),
                        (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<List<string>>(
                        value,
                        (JsonSerializerOptions?)null)
                        ?? new List<string>()))
            .HasColumnType("nvarchar(max)");

        var attachmentsComparer =
            new ValueComparer<List<string>>(
                (a, b) =>
                    a != null &&
                    b != null &&
                    a.SequenceEqual(b),
                value =>
                    value.Aggregate(
                        0,
                        (hash, item) =>
                            HashCode.Combine(
                                hash,
                                item.GetHashCode())),
                value => value.ToList());

        builder.Property(x => x.Attachments)
            .Metadata.SetValueComparer(attachmentsComparer);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.ConversationId,
            x.CreatedAt
        });

        builder.HasIndex(x => x.SenderId);

        builder.HasOne(x => x.Conversation)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Sender)
            .WithMany()
            .HasForeignKey(x => x.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}