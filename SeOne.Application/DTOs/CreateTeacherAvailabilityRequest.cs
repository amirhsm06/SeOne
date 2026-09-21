using System.ComponentModel.DataAnnotations;

namespace SeOne.Application.DTOs;

public class CreateTeacherAvailabilityRequest
{
    public DayOfWeek DayOfWeek { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    public bool IsAvailable { get; set; } = true;
}
