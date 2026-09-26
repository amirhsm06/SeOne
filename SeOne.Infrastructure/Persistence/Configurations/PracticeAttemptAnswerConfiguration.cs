using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class PracticeAttemptAnswerConfiguration : IEntityTypeConfiguration<PracticeAttemptAnswer>
{
    public void Configure(EntityTypeBuilder<PracticeAttemptAnswer> builder)
    {
        builder.ToTable("PracticeAttemptAnswers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AttemptId)
            .IsRequired();

        builder.Property(x => x.QuestionId)
            .IsRequired();

        builder.Property(x => x.Answer)
            .IsRequired()
            .HasMaxLength(10000);

        builder.Property(x => x.IsCorrect)
            .IsRequired();

        builder.Property(x => x.PointsEarned)
            .IsRequired();

        builder.HasIndex(x => x.AttemptId);
        builder.HasIndex(x => new { x.AttemptId, x.QuestionId })
            .IsUnique();
        builder.HasIndex(x => x.QuestionId);

        builder.HasOne(x => x.Attempt)
            .WithMany(x => x.Answers)
            .HasForeignKey(x => x.AttemptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Question)
            .WithMany()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
