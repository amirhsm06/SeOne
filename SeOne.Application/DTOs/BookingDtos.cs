namespace SeOne.Application.DTOs;

public class BookingDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid? CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
