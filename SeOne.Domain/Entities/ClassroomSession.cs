using System;
using System.Collections.Generic;

namespace SeOne.Domain.Entities;

public sealed class ClassroomSession
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public Guid TeacherId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public int Duration { get; set; }
    public string Status { get; set; } = "scheduled";
    public string Provider { get; set; } = "adobe_connect";
    public string? MeetingUrl { get; set; }
    public string? RecordingUrl { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Course Course { get; set; } = null!;
    public User Teacher { get; set; } = null!;
    public ICollection<ClassroomMaterial> Materials { get; set; } = new List<ClassroomMaterial>();
    public ICollection<ClassroomAttendance> Attendance { get; set; } = new List<ClassroomAttendance>();
}
