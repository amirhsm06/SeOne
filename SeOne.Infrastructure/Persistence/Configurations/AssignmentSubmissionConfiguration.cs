using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class AssignmentSubmissionConfiguration
    : IEntityTypeConfiguration<AssignmentSubmission>
{
    public void Configure(
        EntityTypeBuilder<AssignmentSubmission> builder)
    {
        builder.ToTable("AssignmentSubmissions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AssignmentId)
            .IsRequired();

        builder.Property(x => x.StudentId)
            .IsRequired();

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(20000);

        builder.Property(x => x.Attachments)
            .HasConversion(
                new ValueConverter<List<string>, string>(
                    value => JsonSerializer.Serialize(
                        value ?? new List<string>(),
                        (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<List<string>>(
                        value,
                        (JsonSerializerOptions?)null)
                        ?? new List<string>()));

        var attachmentsComparer =
            new ValueComparer<List<string>>(
                (a, b) =>
                    a != null &&
                    b != null &&
                    a.SequenceEqual(b),
                value =>
                    value.Aggregate(
                        0,
                        (hash, item) =>
                            HashCode.Combine(
                                hash,
                                item.GetHashCode())),
                value =>
                    value.ToList());

        builder.Property(x => x.Attachments)
            .Metadata.SetValueComparer(attachmentsComparer);

        builder.Property(x => x.SubmittedAt)
            .IsRequired();

        builder.Property(x => x.Grade)
            .HasPrecision(18, 2);

        builder.Property(x => x.Feedback)
            .HasMaxLength(5000);

        builder.Property(x => x.GradedAt);

        builder.Property(x => x.GradedById);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.AssignmentId,
            x.StudentId
        })
        .IsUnique();

        builder.HasIndex(x => x.StudentId);

        builder.HasIndex(x => x.AssignmentId);

        builder.HasOne(x => x.Assignment)
            .WithMany(x => x.Submissions)
            .HasForeignKey(x => x.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.GradedBy)
            .WithMany()
            .HasForeignKey(x => x.GradedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}