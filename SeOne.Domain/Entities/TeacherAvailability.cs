using System;

namespace SeOne.Domain.Entities;

public class TeacherAvailability
{
    public Guid Id { get; set; }

    public Guid TeacherId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public bool IsAvailable { get; set; }

    public DateTime CreatedAt { get; set; }

    public User Teacher { get; set; } = null!;
}
