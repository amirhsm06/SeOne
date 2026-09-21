namespace SeOne.Application.DTOs;

public class CourseDetailsLessonDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Order { get; set; }
}

public class CourseDetailsModuleDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<CourseDetailsLessonDto> Lessons { get; set; } = new();
}

public class TeacherProfileSummaryDto
{
    public Guid TeacherId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string? TeachingLanguage { get; set; }
    public string? Subject { get; set; }
    public string? Level { get; set; }
    public decimal Rating { get; set; }
}

public class CourseDetailsDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Duration { get; set; }
    public string? ImageUrl { get; set; }
    public Guid TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;

    // teacher profile summary if available
    public TeacherProfileSummaryDto? TeacherProfile { get; set; }

    public int TotalLessonCount { get; set; }
    public int TotalModuleCount { get; set; }

    // student-specific
    public bool? IsEnrolled { get; set; }
    public decimal? ProgressPercentage { get; set; }

    // teacher-specific
    public bool? IsOwner { get; set; }
    public List<CourseDetailsModuleDto>? Modules { get; set; }
}
