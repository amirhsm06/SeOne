using System;

namespace SeOne.Domain.Entities;

public class OtpCode
{
    public Guid Id { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string CodeHash { get; set; } = string.Empty;

    public string Salt { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public int FailedAttempts { get; set; }
}