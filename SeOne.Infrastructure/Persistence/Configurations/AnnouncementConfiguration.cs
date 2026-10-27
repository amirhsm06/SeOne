using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class AnnouncementConfiguration
    : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("Announcements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Message)
            .IsRequired()
            .HasMaxLength(1200);

        builder.Property(x => x.Audience)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.Kind)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.ActionUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.ActionLabel)
            .HasMaxLength(200);

        builder.Property(x => x.PublishAt);
        builder.Property(x => x.ExpiresAt);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => new { x.Status, x.PublishAt });
        builder.HasIndex(x => new { x.Audience, x.Status });
        builder.HasIndex(x => x.ExpiresAt);

        builder.HasMany(x => x.UserStates)
            .WithOne(x => x.Announcement)
            .HasForeignKey(x => x.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
