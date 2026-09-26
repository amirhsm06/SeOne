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
}