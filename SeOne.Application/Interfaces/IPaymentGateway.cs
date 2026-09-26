namespace SeOne.Application.Interfaces;

public interface IPaymentGateway
{
    Task<PaymentGatewayIntent> CreateIntentAsync(
        decimal amount,
        string currency,
        Guid paymentId,
        Guid courseId);

    Task<bool> ConfirmAsync(
        string clientSecret);
}

public class PaymentGatewayIntent
{
    public string ClientSecret { get; set; } = string.Empty;

    public string ProviderPaymentId { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;
}