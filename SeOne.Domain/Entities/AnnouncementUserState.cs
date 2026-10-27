using System;

namespace SeOne.Domain.Entities;

public class AnnouncementUserState
{
    public Guid AnnouncementId { get; set; }

    public Guid UserId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public bool IsDismissed { get; set; }

    public DateTime? DismissedAt { get; set; }

    public Announcement Announcement { get; set; } = null!;

    public User User { get; set; } = null!;
}
