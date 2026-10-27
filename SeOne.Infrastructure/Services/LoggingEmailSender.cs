using Microsoft.Extensions.Logging;
using SeOne.Application.Interfaces;

namespace SeOne.Infrastructure.Services;

public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(
        ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetAsync(
        string email,
        string fullName,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogWarning(
            "Password-reset email delivery is not configured; no email was sent.");

        throw new SeOne.Application.Exceptions.DeliveryNotConfiguredException(
            "Password-reset email delivery is not configured.");
    }
}