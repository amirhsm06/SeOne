namespace SeOne.Application.DTOs;

public class CourseDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string Currency { get; set; } = "IRR";

    public decimal DiscountPercent { get; set; }

    public string? Duration { get; set; }

    public string? ImageUrl { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Language { get; set; } = "en";

    public bool IsFeatured { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid TeacherId { get; set; }

    public string TeacherName { get; set; } = string.Empty;
}