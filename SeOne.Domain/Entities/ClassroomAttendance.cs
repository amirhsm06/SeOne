using System;

namespace SeOne.Domain.Entities;

public sealed class ClassroomAttendance
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public string Status { get; set; } = "present";
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }

    public ClassroomSession Session { get; set; } = null!;
    public User Student { get; set; } = null!;
}
