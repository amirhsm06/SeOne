namespace SeOne.Domain.Entities;

public sealed class CourseClass
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public Guid TeacherId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Type { get; set; } = "public";
    public int MaxCapacity { get; set; }
    public decimal BasePrice { get; set; }
    public DateTime ScheduledDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public string? MeetingUrl { get; set; }
    public string Status { get; set; } = "scheduled";
    public DateTime CreatedAt { get; set; }

    public Course Course { get; set; } = null!;
    public User Teacher { get; set; } = null!;
    public ICollection<ClassEnrollment> Enrollments { get; set; } = new List<ClassEnrollment>();
    public ICollection<ClassInvite> Invites { get; set; } = new List<ClassInvite>();
}
