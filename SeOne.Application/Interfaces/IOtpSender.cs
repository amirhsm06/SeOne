namespace SeOne.Application.Interfaces;

public interface IOtpSender
{
    bool IsConfigured { get; }
    Task SendAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken = default);
}