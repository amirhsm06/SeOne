using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public sealed class TeacherWithdrawalConfiguration : IEntityTypeConfiguration<TeacherWithdrawal>
{
    public void Configure(EntityTypeBuilder<TeacherWithdrawal> builder)
    {
        builder.ToTable("TeacherWithdrawals");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(x => x.Iban)
            .HasMaxLength(26)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.RequestedAt).IsRequired();
        builder.Property(x => x.DecidedAt);

        builder.HasIndex(x => new { x.TeacherId, x.Status, x.RequestedAt });
        builder.HasIndex(x => new { x.Status, x.RequestedAt });

        builder.HasOne(x => x.Teacher)
            .WithMany()
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DecidedBy)
            .WithMany()
            .HasForeignKey(x => x.DecidedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}