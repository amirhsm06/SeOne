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
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = "IRR";
    public decimal DiscountPercent { get; set; }
    public string? Duration { get; set; }
    public string? ImageUrl { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public bool IsFeatured { get; set; }
}
