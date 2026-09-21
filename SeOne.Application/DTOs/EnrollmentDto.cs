namespace SeOne.Application.DTOs;

public class EnrollmentDto
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public string CourseTitle { get; set; } = string.Empty;

    public Guid StudentId { get; set; }

    public DateTime EnrolledAt { get; set; }
}
