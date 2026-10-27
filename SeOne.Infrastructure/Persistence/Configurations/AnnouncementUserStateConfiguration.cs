using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class AnnouncementUserStateConfiguration
    : IEntityTypeConfiguration<AnnouncementUserState>
{
    public void Configure(EntityTypeBuilder<AnnouncementUserState> builder)
    {
        builder.ToTable("AnnouncementUserStates");

        builder.HasKey(x => new { x.AnnouncementId, x.UserId });

        builder.Property(x => x.IsRead).IsRequired();
        builder.Property(x => x.ReadAt);
        builder.Property(x => x.IsDismissed).IsRequired();
        builder.Property(x => x.DismissedAt);

        builder.HasIndex(x => new { x.UserId, x.IsDismissed });
        builder.HasIndex(x => new { x.UserId, x.IsRead });

        builder.HasOne(x => x.Announcement)
            .WithMany(x => x.UserStates)
            .HasForeignKey(x => x.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
