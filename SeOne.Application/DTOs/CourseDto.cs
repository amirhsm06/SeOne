namespace SeOne.Application.DTOs;

public class CourseDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string? Duration { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid TeacherId { get; set; }

    public string TeacherName { get; set; } = string.Empty;
}