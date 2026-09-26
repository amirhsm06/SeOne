using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(x => x.Bio)
            .HasMaxLength(5000);

        builder.Property(x => x.Country)
            .HasMaxLength(100);

        builder.Property(x => x.Language)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Timezone)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.AccountStatus)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.TwoFactorMethod)
            .HasMaxLength(20);

        builder.Property(x => x.TwoFactorBackupCodesJson)
            .HasMaxLength(2000);

        builder.Property(x => x.AccountDeletionToken)
            .HasMaxLength(100);

        builder.Property(x => x.AccountDeletionScheduledAt)
            .IsRequired(false);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => x.AccountStatus);
        builder.HasIndex(x => x.AccountDeletionToken);
    }
}
