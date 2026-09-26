using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SeOne.Application.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Type { get; set; } = "system";

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Dictionary<string, object?>? Data { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? ActionUrl { get; set; }

    public string? ActionLabel { get; set; }
}

public class NotificationStatsDto
{
    public int Total { get; set; }

    public int Unread { get; set; }

    public NotificationTypeStatsDto ByType { get; set; } = new();
}

public class NotificationTypeStatsDto
{
    public int Booking { get; set; }

    public int Payment { get; set; }

    public int Assignment { get; set; }

    public int Message { get; set; }

    public int Promotion { get; set; }

    public int System { get; set; }
}

public class CreateTestNotificationRequest
{
    [Required]
    public string Type { get; set; } = "system";
}

public class NotificationActionResultDto
{
    public bool Success { get; set; }

    public string? RedirectUrl { get; set; }
}