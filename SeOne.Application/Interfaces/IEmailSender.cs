namespace SeOne.Application.Interfaces;

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendPasswordResetAsync(
        string email,
        string fullName,
        string resetToken,
        CancellationToken cancellationToken = default);
}