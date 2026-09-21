namespace SeOne.Application.DTOs;

public class ModuleLearningDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Order { get; set; }

    public int TotalLessons { get; set; }

    public int CompletedLessons { get; set; }

    public decimal Percentage { get; set; }

    public List<LessonLearningDto> Lessons { get; set; } = new List<LessonLearningDto>();
}
