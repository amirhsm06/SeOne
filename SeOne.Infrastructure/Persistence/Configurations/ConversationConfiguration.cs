using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class ConversationConfiguration
    : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .IsRequired();

        builder.Property(x => x.Subject)
            .HasMaxLength(300);

        builder.Property(x => x.RelatedCourseId);

        builder.Property(x => x.RelatedAssignmentId);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => x.UpdatedAt);

        builder.HasIndex(x => x.RelatedCourseId);

        builder.HasIndex(x => x.RelatedAssignmentId);

        builder.HasOne(x => x.RelatedCourse)
            .WithMany()
            .HasForeignKey(x => x.RelatedCourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RelatedAssignment)
            .WithMany()
            .HasForeignKey(x => x.RelatedAssignmentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}