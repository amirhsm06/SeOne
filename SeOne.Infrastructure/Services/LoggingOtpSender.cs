using Microsoft.Extensions.Logging;
using SeOne.Application.Interfaces;

namespace SeOne.Infrastructure.Services;

public class LoggingOtpSender : IOtpSender
{
    private readonly ILogger<LoggingOtpSender> _logger;

    public LoggingOtpSender(ILogger<LoggingOtpSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogWarning(
            "OTP delivery is not configured; the verification code was not sent.");

        throw new SeOne.Application.Exceptions.DeliveryNotConfiguredException(
            "OTP delivery is not configured. Configure an SMS provider before requesting verification codes.");
    }
}