using System;
using System.Collections.Generic;

namespace SeOne.Domain.Entities;

public class Announcement
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Audience { get; set; } = "all";

    public string Kind { get; set; } = "info";

    public string Status { get; set; } = "draft";

    public string? ActionUrl { get; set; }

    public string? ActionLabel { get; set; }

    public DateTime? PublishAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<AnnouncementUserState> UserStates { get; set; }
        = new List<AnnouncementUserState>();
}
