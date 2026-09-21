namespace SeOne.Application.DTOs;

public class TeacherCourseStudentDto
{
    public Guid StudentId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime EnrolledAt { get; set; }

    public int TotalLessons { get; set; }

    public int CompletedLessons { get; set; }

    public decimal ProgressPercentage { get; set; }
}
