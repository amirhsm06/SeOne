using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class ReviewHelpfulConfiguration
    : IEntityTypeConfiguration<ReviewHelpful>
{
    public void Configure(EntityTypeBuilder<ReviewHelpful> builder)
    {
        builder.ToTable("ReviewHelpful");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReviewId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.ReviewId,
            x.UserId
        })
        .IsUnique();

        builder.HasOne(x => x.Review)
            .WithMany(x => x.HelpfulMarks)
            .HasForeignKey(x => x.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}