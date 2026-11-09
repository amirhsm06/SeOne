namespace SeOne.Application.DTOs;

public sealed class CreateClassSlotRequest
{
    public Guid CourseId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string ClassType { get; set; } = "public";
    public int Capacity { get; set; }
    public decimal BasePrice { get; set; }
    public decimal? FinalPrice { get; set; }
    public string Currency { get; set; } = "IRR";
}

public sealed class AssignClassSlotTeacherRequest
{
    public Guid TeacherId { get; set; }
}

public sealed class ClassSlotDto
{
    public Guid Id { get; set; }
    public Guid SlotId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? AssignmentId { get; set; }
    public Guid? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public string? TeacherAvatarUrl { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string ClassType { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int EnrollmentCount { get; set; }
    public int CurrentEnrollment { get; set; }
    public decimal BasePrice { get; set; }
    public decimal FinalPrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? CourseTitle { get; set; }
    public string? CourseImageUrl { get; set; }
}

public sealed class AvailableClassSlotTeacherDto
{
    public Guid TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string? TeacherAvatarUrl { get; set; }
}
