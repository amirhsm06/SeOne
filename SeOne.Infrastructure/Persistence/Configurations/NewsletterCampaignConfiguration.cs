using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class NewsletterCampaignConfiguration : IEntityTypeConfiguration<NewsletterCampaign>
{
    public void Configure(EntityTypeBuilder<NewsletterCampaign> builder)
    {
        builder.ToTable("NewsletterCampaigns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Content).IsRequired().HasMaxLength(50000);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(30);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ScheduledAt).IsRequired(false);
        builder.Property(x => x.SentAt).IsRequired(false);
        builder.HasIndex(x => new { x.Status, x.ScheduledAt });
    }
}
