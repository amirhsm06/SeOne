namespace SeOne.Application.Interfaces;

public interface IEmailSender
{
    Task SendPasswordResetAsync(
        string email,
        string fullName,
        string resetToken,
        CancellationToken cancellationToken = default);
}