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
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Level { get; set; }
    public string? TeacherRole { get; set; }
}

public sealed class AssignClassSlotTeacherRequest
{
    public Guid TeacherId { get; set; }
}

public sealed class UpdateClassSlotRequest
{
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string? ClassType { get; set; }
    public int? Capacity { get; set; }
    public decimal? BasePrice { get; set; }
    public decimal? FinalPrice { get; set; }
    public string? Currency { get; set; }
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
    public Guid DestinationSlotId { get; set; }
    // Current frontend sends targetSlotId; retain DestinationSlotId for older clients.
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
    public decimal BasePrice { get; set; }
    public decimal FinalPrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "UTC";
    public string Status { get; set; } = string.Empty;
    public int EnrolledCount { get; set; }
    public int RemainingSeats { get; set; }
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
    public Guid TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string? TeacherAvatarUrl { get; set; }
}
