using SeOne.Application.Interfaces;

namespace SeOne.Infrastructure.Services;

/// <summary>
/// Retained only so existing code/configuration references remain source-compatible.
/// It deliberately does not simulate a payment provider or report successful payments.
/// Use a real gateway implementation when a provider has been selected and configured.
/// </summary>
[Obsolete("Development payment simulation is disabled. Register a real provider or UnavailablePaymentGateway.")]
public sealed class DevelopmentPaymentGateway : IPaymentGateway
{
    public bool IsConfigured => false;

    public Task<PaymentGatewayIntent> CreateIntentAsync(
        decimal amount,
        string currency,
        Guid paymentId,
        Guid courseId)
        => Task.FromException<PaymentGatewayIntent>(
            new InvalidOperationException("Development payment simulation is disabled."));

    public Task<bool> ConfirmAsync(string clientSecret)
        => Task.FromException<bool>(
            new InvalidOperationException("Development payment simulation is disabled."));
}
