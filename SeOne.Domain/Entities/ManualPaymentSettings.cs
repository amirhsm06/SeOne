using System;

namespace SeOne.Domain.Entities
{
    /// <summary>
    /// Global system settings for manual payment receipts (e.g., via WhatsApp proof).
    /// </summary>
    public class ManualPaymentSettings
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Whether manual receipt-based payments are enabled for class enrollments.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Maximum allowed file size in bytes for receipt uploads (e.g., 5MB = 5242880).
        /// </summary>
        public long MaxReceiptSizeBytes { get; set; }

        /// <summary>
        /// Comma-separated allowed MIME types (e.g., "image/png,image/jpeg,application/pdf").
        /// </summary>
        public string AllowedMimeTypes { get; set; } = "image/png,image/jpeg,image/gif,application/pdf";

        /// <summary>
        /// WhatsApp business number or contact link for students to reference when sending proof.
        /// </summary>
        public string WhatsAppContactNumber { get; set; } = "";

        /// <summary>
        /// Last updated timestamp.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}
