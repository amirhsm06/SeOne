using System;
using System.Collections.Generic;

namespace SeOne.Application.DTOs
{
    /// <summary>
    /// Request to upload a manual payment receipt (WhatsApp screenshot or proof).
    /// </summary>
    public class UploadReceiptRequest
    {
        /// <summary>
        /// Base64-encoded file content or raw file bytes will be supplied via multipart/form-data.
        /// </summary>
        public byte[] FileContent { get; set; }

        /// <summary>
        /// Original filename (e.g., "proof.png").
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// MIME type of the uploaded file (e.g., "image/png").
        /// </summary>
        public string MimeType { get; set; }

        /// <summary>
        /// Optional note from student about the payment method or timestamp.
        /// </summary>
        public string Note { get; set; }
    }

    /// <summary>
    /// Response after receipt upload.
    /// </summary>
    public class ReceiptUploadResponse
    {
        public Guid PaymentId { get; set; }
        public Guid ClassEnrollmentId { get; set; }
        public string ReceiptObjectPath { get; set; }
        public string Status { get; set; }
        public DateTime UploadedAt { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Admin request to verify a manually-uploaded receipt.
    /// </summary>
    public class VerifyReceiptRequest
    {
        /// <summary>
        /// true to approve, false to reject.
        /// </summary>
        public bool IsApproved { get; set; }

        /// <summary>
        /// If rejected, reason explanation.
        /// </summary>
        public string RejectionReason { get; set; }
    }

    /// <summary>
    /// Response for receipt verification.
    /// </summary>
    public class VerifyReceiptResponse
    {
        public Guid PaymentId { get; set; }
        public string Status { get; set; }
        public DateTime VerifiedAt { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// List item for manual payment receipts pending verification.
    /// </summary>
    public class PendingReceiptDto
    {
        public Guid PaymentId { get; set; }
        public Guid ClassEnrollmentId { get; set; }
        public Guid StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentEmail { get; set; }
        public decimal Amount { get; set; }
        public string ClassName { get; set; }
        public string ReceiptObjectPath { get; set; }
        public DateTime UploadedAt { get; set; }
        public string ReceiptDownloadUrl { get; set; }

        /// <summary>
        /// Link to view receipt in Supabase storage or signed URL.
        /// </summary>
        public string PreviewUrl { get; set; }
    }

    /// <summary>
    /// Manual payment settings response for students and admins.
    /// </summary>
    public class ManualPaymentSettingsDto
    {
        public bool IsEnabled { get; set; }
        public long MaxReceiptSizeBytes { get; set; }
        public string AllowedMimeTypes { get; set; }
        public string WhatsAppContactNumber { get; set; }
        public string WhatsAppLink { get; set; }
    }
}
