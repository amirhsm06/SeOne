using System;

namespace SeOne.Domain.Entities;

public class Booking
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid TeacherId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public BookingStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public User Student { get; set; } = null!;

    public User Teacher { get; set; } = null!;
}

public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    Cancelled = 2,
    Completed = 3
}
