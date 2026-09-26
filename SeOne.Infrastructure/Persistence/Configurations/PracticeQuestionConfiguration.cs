using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class PracticeQuestionConfiguration : IEntityTypeConfiguration<PracticeQuestion>
{
    public void Configure(EntityTypeBuilder<PracticeQuestion> builder)
    {
        builder.ToTable("PracticeQuestions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PracticeId)
            .IsRequired();

        builder.Property(x => x.QuestionText)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(x => x.QuestionType)
            .IsRequired();

        builder.Property(x => x.OptionsJson)
            .HasMaxLength(10000);

        builder.Property(x => x.CorrectAnswer)
            .IsRequired()
            .HasMaxLength(10000);

        builder.Property(x => x.Explanation)
            .HasMaxLength(5000);

        builder.Property(x => x.Points)
            .IsRequired();

        builder.Property(x => x.Order)
            .IsRequired();

        builder.HasIndex(x => x.PracticeId);
        builder.HasIndex(x => new { x.PracticeId, x.Order })
            .IsUnique();

        builder.HasOne(x => x.Practice)
            .WithMany(x => x.Questions)
            .HasForeignKey(x => x.PracticeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
