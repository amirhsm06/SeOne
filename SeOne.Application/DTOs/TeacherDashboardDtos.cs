namespace SeOne.Application.DTOs;

public class TeacherCourseSummaryDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public bool IsPublished { get; set; }

    public int StudentCount { get; set; }

    public int LessonCount { get; set; }

    public decimal Price { get; set; }

    public string? Duration { get; set; }

    public string? ImageUrl { get; set; }
}

public class TeacherAvailabilityItemDto
{
    public Guid Id { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsAvailable { get; set; }
}

public class TeacherDashboardDto
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Avatar { get; set; }

    public string? TeachingLanguage { get; set; }

    public string? Subject { get; set; }

    public string? Level { get; set; }

    public decimal Rating { get; set; }

    // summary
    public int TotalCourses { get; set; }

    public int PublishedCourses { get; set; }

    public int TotalStudents { get; set; }

    public List<TeacherCourseSummaryDto> Courses { get; set; } = new();

    public List<TeacherAvailabilityItemDto> Availability { get; set; } = new();
}
