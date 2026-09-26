using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class SupportMessageConfiguration : IEntityTypeConfiguration<SupportMessage>
{
    public void Configure(EntityTypeBuilder<SupportMessage> builder)
    {
        builder.ToTable("SupportMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserName).HasMaxLength(200);
        builder.Property(x => x.Content).IsRequired().HasMaxLength(10000);
        builder.Property(x => x.IsFromSupport).IsRequired();
        builder.Property(x => x.IsInternal).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        var converter = new ValueConverter<List<string>, string>(
            value => JsonSerializer.Serialize(value ?? new List<string>(), (JsonSerializerOptions?)null),
            value => JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions?)null) ?? new List<string>());
        builder.Property(x => x.Attachments).HasConversion(converter);
        builder.Property(x => x.Attachments).Metadata.SetValueComparer(new ValueComparer<List<string>>(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item == null ? 0 : item.GetHashCode())),
            value => value.ToList()));

        builder.HasIndex(x => new { x.TicketId, x.CreatedAt });
        builder.HasOne(x => x.Ticket).WithMany(x => x.Messages).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
