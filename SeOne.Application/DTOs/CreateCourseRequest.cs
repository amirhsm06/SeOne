namespace SeOne.Application.DTOs;

using System.ComponentModel.DataAnnotations;
using SeOne.Application.Validation;

public class CreateCourseRequest
{
    [Required]
    [MaxLength(200)]
    [NotEmptyOrWhitespace]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    [NotEmptyOrWhitespace]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [NotEmptyOrWhitespace]
    public string Level { get; set; } = string.Empty;

    [Range(0, 1000000)]
    public decimal Price { get; set; }

    [MaxLength(100)]
    public string? Duration { get; set; }

    [MaxLength(500)]
    [SeOne.Application.Validation.HttpOrHttpsUrl]
    public string? ImageUrl { get; set; }
}
