using System.Security.Cryptography;
using SeOne.Application.Interfaces;

namespace SeOne.Infrastructure.Services;

public class DevelopmentPaymentGateway : IPaymentGateway
{
    public Task<PaymentGatewayIntent> CreateIntentAsync(
        decimal amount,
        string currency,
        Guid paymentId,
        Guid courseId)
    {
        var clientSecret =
            "dev_secret_" +
            Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32))
            .ToLowerInvariant();

        var providerPaymentId =
            "dev_pi_" +
            Guid.NewGuid().ToString("N");

        return Task.FromResult(
            new PaymentGatewayIntent
            {
                ClientSecret = clientSecret,
                ProviderPaymentId = providerPaymentId,
                Provider = "Development"
            });
    }

    public Task<bool> ConfirmAsync(
        string clientSecret)
    {
        return Task.FromResult(
            !string.IsNullOrWhiteSpace(clientSecret) &&
            clientSecret.StartsWith(
                "dev_secret_",
                StringComparison.Ordinal));
    }
}