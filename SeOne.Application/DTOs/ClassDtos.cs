namespace SeOne.Application.DTOs;

public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class CourseClassDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int MaxCapacity { get; set; }
    public int CurrentEnrollment { get; set; }
    public int RemainingCapacity { get; set; }
    public decimal BasePrice { get; set; }
    public decimal FinalPrice { get; set; }
    public DateTime ScheduledDate { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "UTC";
    public string Status { get; set; } = string.Empty;
    public ClassTeacherDto Teacher { get; set; } = new();
    public ClassCourseDto Course { get; set; } = new();
}

public sealed class ClassTeacherDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public decimal Rating { get; set; }
}

public sealed class ClassCourseDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}

public sealed class CreateCourseClassRequest
{
    public Guid CourseId { get; set; }
    public Guid? TeacherId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Level { get; set; }
    public string? Subject { get; set; }
    public string Type { get; set; } = "public";
    public int MaxCapacity { get; set; }
    public decimal? BasePrice { get; set; }
    public DateTime ScheduledDate { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public string? MeetingUrl { get; set; }
}

public sealed class EnrollInClassRequest
{
    public string? InviteCode { get; set; }
    public Guid? InvitedByStudentId { get; set; }
}
