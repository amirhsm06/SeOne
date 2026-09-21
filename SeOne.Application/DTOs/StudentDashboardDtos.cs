namespace SeOne.Application.DTOs;

public class StudentDashboardCourseDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid TeacherId { get; set; }

    public string TeacherName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string? Duration { get; set; }

    public string? ImageUrl { get; set; }

    public DateTime EnrolledAt { get; set; }

    public int TotalLessons { get; set; }

    public int CompletedLessons { get; set; }

    public decimal ProgressPercentage { get; set; }
}

public class StudentDashboardDto
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    // summary
    public int EnrolledCourseCount { get; set; }

    public int CompletedCourseCount { get; set; }

    public decimal OverallProgressPercentage { get; set; }

    public List<StudentDashboardCourseDto> Courses { get; set; } = new();
}
