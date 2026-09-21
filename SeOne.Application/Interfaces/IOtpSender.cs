namespace SeOne.Application.Interfaces;

public interface IOtpSender
{
    Task SendAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken = default);
}