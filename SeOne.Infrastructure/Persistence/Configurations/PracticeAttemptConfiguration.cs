using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class PracticeAttemptConfiguration : IEntityTypeConfiguration<PracticeAttempt>
{
    public void Configure(EntityTypeBuilder<PracticeAttempt> builder)
    {
        builder.ToTable("PracticeAttempts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PracticeId)
            .IsRequired();

        builder.Property(x => x.StudentId)
            .IsRequired();

        builder.Property(x => x.Score)
            .IsRequired();

        builder.Property(x => x.MaxScore)
            .IsRequired();

        builder.Property(x => x.StartedAt)
            .IsRequired();

        builder.Property(x => x.SubmittedAt)
            .IsRequired(false);

        builder.HasIndex(x => x.PracticeId);
        builder.HasIndex(x => x.StudentId);
        builder.HasIndex(x => new { x.StudentId, x.SubmittedAt });

        builder.HasOne(x => x.Practice)
            .WithMany(x => x.Attempts)
            .HasForeignKey(x => x.PracticeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
