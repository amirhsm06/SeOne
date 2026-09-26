using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class AssignmentConfiguration
    : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("Assignments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CourseId)
            .IsRequired();

        builder.Property(x => x.CourseModuleId)
            .IsRequired(false);

        builder.Property(x => x.TeacherId)
            .IsRequired();

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(x => x.Instructions)
            .HasMaxLength(5000);

        builder.Property(x => x.DueDate)
            .IsRequired();

        builder.Property(x => x.MaxPoints)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Attachments)
            .HasConversion(
                new ValueConverter<List<string>, string>(
                    value => JsonSerializer.Serialize(
                        value ?? new List<string>(),
                        (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<List<string>>(
                        value,
                        (JsonSerializerOptions?)null)
                        ?? new List<string>()))
            .HasColumnType("nvarchar(max)");

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

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.Property(x => x.IsPublished)
            .IsRequired();

        builder.HasIndex(x => x.CourseId);

        builder.HasIndex(x => x.CourseModuleId);

        builder.HasIndex(x => x.TeacherId);

        builder.HasIndex(x => new
        {
            x.TeacherId,
            x.IsPublished
        });

        builder.HasOne(x => x.Course)
            .WithMany()
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CourseModule)
            .WithMany()
            .HasForeignKey(x => x.CourseModuleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Teacher)
            .WithMany()
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}