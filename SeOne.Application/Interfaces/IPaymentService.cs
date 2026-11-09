using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface IPaymentService
{
    Task<PaymentIntentDto?> CreatePaymentIntentAsync(
        Guid studentId,
        Guid courseId);

    Task<PaymentDto?> ConfirmPaymentAsync(
        Guid studentId,
        Guid paymentId,
        string clientSecret);

    Task<PaymentDto?> GetByIdAsync(
        Guid studentId,
        Guid paymentId);

    Task<List<PaymentDto>> GetMyPaymentsAsync(
        Guid studentId);

    /// <summary>
    /// Upload a manual payment receipt (WhatsApp proof file) for a class enrollment.
    /// </summary>
    Task<ReceiptUploadResponse?> UploadReceiptAsync(
        Guid studentId,
        Guid classEnrollmentId,
        string fileName,
        string mimeType,
        Stream fileStream,
        string note);

    Task<ReceiptUploadResponse?> UploadReceiptForPaymentAsync(
        Guid studentId,
        Guid paymentId,
        string fileName,
        string mimeType,
        Stream fileStream,
        string note);

    /// <summary>
    /// Get all pending manual payment receipts awaiting admin verification.
    /// </summary>
    Task<List<PendingReceiptDto>> GetPendingReceiptsAsync();

    /// <summary>
    /// Admin: Verify or reject a manual payment receipt.
    /// </summary>
    Task<VerifyReceiptResponse?> VerifyReceiptAsync(
        Guid paymentId,
        Guid adminId,
        bool isApproved,
        string? rejectionReason);

    /// <summary>
    /// Get manual payment settings (enabled, file size limits, WhatsApp link).
    /// </summary>
    Task<ManualPaymentSettingsDto> GetManualPaymentSettingsAsync();
}