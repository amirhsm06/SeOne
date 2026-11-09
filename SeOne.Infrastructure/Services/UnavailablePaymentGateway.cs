using SeOne.Application.Interfaces;

namespace SeOne.Infrastructure.Services;

/// <summary>
/// Safe default until a real payment provider is selected and configured.
/// This implementation never creates a simulated successful payment.
/// </summary>
public sealed class UnavailablePaymentGateway : IPaymentGateway
{
    public bool IsConfigured => false;

    public Task<PaymentGatewayIntent> CreateIntentAsync(
        decimal amount,
        string currency,
        Guid paymentId,
        Guid courseId)
        => throw new InvalidOperationException("No payment gateway is configured.");

    public Task<bool> ConfirmAsync(string clientSecret)
        => throw new InvalidOperationException("No payment gateway is configured.");
}
