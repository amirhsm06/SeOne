using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Persistence.Configurations
{
    public class ManualPaymentSettingsConfiguration : IEntityTypeConfiguration<ManualPaymentSettings>
    {
        public void Configure(EntityTypeBuilder<ManualPaymentSettings> builder)
        {
            builder.ToTable("ManualPaymentSettings");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.MaxReceiptSizeBytes)
                .IsRequired();

            builder.Property(x => x.AllowedMimeTypes)
                .HasMaxLength(1000)
                .IsRequired();

            builder.Property(x => x.WhatsAppContactNumber)
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .IsRequired();

            builder.Property(x => x.IsEnabled)
                .IsRequired();
        }
    }
}
