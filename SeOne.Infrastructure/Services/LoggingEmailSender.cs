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
        _logger.LogInformation(
            """
            SE ONE password reset requested.
            Email: {Email}
            FullName: {FullName}
            Reset Token: {ResetToken}
            """,
            email,
            fullName,
            resetToken);

        return Task.CompletedTask;
    }
}