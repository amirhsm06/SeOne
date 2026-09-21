namespace SeOne.Application.DTOs;

public class CourseProgressDto
{
    public Guid CourseId { get; set; }

    public int TotalLessons { get; set; }

    public int CompletedLessons { get; set; }

    public double Percentage { get; set; }
}
