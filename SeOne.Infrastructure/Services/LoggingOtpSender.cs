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
        _logger.LogInformation(
            "SE ONE OTP generated. Phone: {PhoneNumber}, OTP: {OtpCode}",
            phoneNumber,
            code);

        return Task.CompletedTask;
    }
}