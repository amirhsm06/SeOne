namespace SeOne.Application.DTOs;

public class CourseLearningDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid TeacherId { get; set; }

    public string TeacherName { get; set; } = string.Empty;

    public int TotalLessons { get; set; }

    public int CompletedLessons { get; set; }

    public decimal Percentage { get; set; }

    public List<ModuleLearningDto> Modules { get; set; } = new List<ModuleLearningDto>();
}
