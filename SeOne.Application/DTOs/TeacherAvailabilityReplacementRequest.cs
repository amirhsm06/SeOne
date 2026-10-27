namespace SeOne.Application.DTOs;

public sealed class TeacherAvailabilityReplacementRequest
{
    public List<TeacherAvailabilityReplacementSlotRequest>? Slots { get; init; }
}

public sealed class TeacherAvailabilityReplacementSlotRequest
{
    public string? Id { get; init; }

    public string? DayOfWeek { get; init; }

    public string? StartTime { get; init; }

    public string? EndTime { get; init; }
}

public sealed record TeacherAvailabilityReplacementSlotDto(
    Guid? Id,
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime);
