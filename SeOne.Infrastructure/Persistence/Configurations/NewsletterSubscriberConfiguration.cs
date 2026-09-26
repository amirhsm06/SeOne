using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class NewsletterSubscriberConfiguration : IEntityTypeConfiguration<NewsletterSubscriber>
{
    public void Configure(EntityTypeBuilder<NewsletterSubscriber> builder)
    {
        builder.ToTable("NewsletterSubscribers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(320);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(30);
        builder.Property(x => x.ManageToken).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SubscribedAt).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.ManageToken).IsUnique();
    }
}
