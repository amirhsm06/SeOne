namespace SeOne.Application.DTOs;

public class CourseEnrollmentStudentDto
{
    public Guid StudentId { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public string StudentEmail { get; set; } = string.Empty;

    public DateTime EnrolledAt { get; set; }
}
