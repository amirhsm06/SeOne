using System;

namespace SeOne.Application.DTOs;

public class CreateBookingRequest
{
    public Guid TeacherId { get; set; }
    public Guid? SlotId { get; set; }
    public Guid? CourseId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
