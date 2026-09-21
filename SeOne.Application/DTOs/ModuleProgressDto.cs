namespace SeOne.Application.DTOs;

public class ModuleProgressDto
{
    public Guid ModuleId { get; set; }

    public int TotalLessons { get; set; }

    public int CompletedLessons { get; set; }

    public double Percentage { get; set; }
}
