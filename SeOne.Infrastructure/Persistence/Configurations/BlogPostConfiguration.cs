using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.Excerpt)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(b => b.Content)
            .IsRequired();

        builder.Property(b => b.ImageUrl)
            .HasMaxLength(500);

        builder.Property(b => b.Language)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(b => b.IsPublished)
            .IsRequired();

        builder.Property(b => b.CreatedAt)
            .IsRequired();
    }
}
