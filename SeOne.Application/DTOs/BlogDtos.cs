using System;
using System.ComponentModel.DataAnnotations;
using SeOne.Application.Validation;

namespace SeOne.Application.DTOs;

public class BlogPostDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Excerpt { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string Author { get; set; } = string.Empty;

    public string ReadTime { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string Content { get; set; } = string.Empty;
}

public class CreateBlogPostRequest
{
    [Required]
    [MaxLength(200)]
    [NotEmptyOrWhitespace]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    [NotEmptyOrWhitespace]
    public string Excerpt { get; set; } = string.Empty;

    [Required]
    [NotEmptyOrWhitespace]
    public string Content { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [Required]
    [MaxLength(150)]
    [NotEmptyOrWhitespace]
    public string Author { get; set; } = "SE ONE Journal";

    [Required]
    [MaxLength(50)]
    [NotEmptyOrWhitespace]
    public string ReadTime { get; set; } = "5 min";

    [Required]
    [MaxLength(100)]
    [NotEmptyOrWhitespace]
    public string Category { get; set; } = "General";

    [Required]
    [MaxLength(10)]
    [NotEmptyOrWhitespace]
    public string Language { get; set; } = "en";

    public bool IsPublished { get; set; }
}

public class UpdateBlogPostRequest
{
    [Required]
    [MaxLength(200)]
    [NotEmptyOrWhitespace]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    [NotEmptyOrWhitespace]
    public string Excerpt { get; set; } = string.Empty;

    [Required]
    [NotEmptyOrWhitespace]
    public string Content { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [Required]
    [MaxLength(150)]
    [NotEmptyOrWhitespace]
    public string Author { get; set; } = "SE ONE Journal";

    [Required]
    [MaxLength(50)]
    [NotEmptyOrWhitespace]
    public string ReadTime { get; set; } = "5 min";

    [Required]
    [MaxLength(100)]
    [NotEmptyOrWhitespace]
    public string Category { get; set; } = "General";

    [Required]
    [MaxLength(10)]
    [NotEmptyOrWhitespace]
    public string Language { get; set; } = "en";

    public bool IsPublished { get; set; }
}