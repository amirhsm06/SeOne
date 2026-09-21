namespace SeOne.Application.DTOs;

public class LessonDto
{
    public Guid Id { get; set; }

    public Guid CourseModuleId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public int Order { get; set; }

    public DateTime CreatedAt { get; set; }
}
