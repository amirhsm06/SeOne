namespace SeOne.Application.DTOs;

public class CourseCatalogDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int LessonCount { get; set; }

    public int StudentCount { get; set; }

    public decimal Price { get; set; }

    public string? Duration { get; set; }

    public string? ImageUrl { get; set; }
}
