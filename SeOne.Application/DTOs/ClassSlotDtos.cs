namespace SeOne.Application.DTOs;

public sealed class CreateClassSlotRequest
{
    public Guid CourseId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string ClassType { get; set; } = "public";
    public int Capacity { get; set; }
    public decimal BasePrice { get; set; }
    public decimal? FinalPrice { get; set; }
    public string Currency { get; set; } = "IRR";
    public string? TimeZone { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Level { get; set; }
    public string? TeacherRole { get; set; }
}

public sealed class AssignClassSlotTeacherRequest
{
    public Guid? TeacherId { get; set; }
}

public sealed class UpdateClassSlotRequest
{
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public string? ClassType { get; set; }
    public int? Capacity { get; set; }
    public decimal? BasePrice { get; set; }
    public decimal? FinalPrice { get; set; }
    public string? Currency { get; set; }
    public string? TimeZone { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Level { get; set; }
    public string? TeacherRole { get; set; }
}

public sealed class AddClassSlotEnrollmentRequest
{
    public Guid StudentId { get; set; }
}

public sealed class CreateClassSlotAssignmentRequest
{
    public Guid SlotId { get; set; }
    public Guid TeacherId { get; set; }
}

public sealed class UpdateClassSlotAssignmentRequest
{
    public Guid? TeacherId { get; set; }
    public string? Status { get; set; }
}

public sealed class MoveClassEnrollmentRequest
{
    // DestinationSlotId is retained for older clients; the current frontend sends TargetSlotId.
    public Guid DestinationSlotId { get; set; }
    public Guid TargetSlotId { get; set; }
}

public sealed class ClassSlotDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string TeacherRole { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
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
    public int EnrolledCount { get; set; }
    public int RemainingSeats { get; set; }
    public bool Enrolled { get; set; }
    public Guid? ClassGroupId { get; set; }
    public decimal BasePrice { get; set; }
    public decimal FinalPrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "UTC";
    public string Status { get; set; } = string.Empty;
    public List<ClassSlotTeacherAssignmentDto> TeacherAssignments { get; set; } = new();
    public string? CourseTitle { get; set; }
    public string? CourseImageUrl { get; set; }
}

public sealed class ClassSlotTeacherAssignmentDto
{
    public Guid Id { get; set; }
    public Guid SlotId { get; set; }
    public Guid TeacherId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SelectedAt { get; set; }
    public string? TeacherName { get; set; }
    public string? TeacherAvatarUrl { get; set; }
}

public sealed class AvailableClassSlotTeacherDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? TeacherAvatarUrl { get; set; }
    public string TeachingLanguage { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string MatchedAvailability { get; set; } = string.Empty;
}
