using System;

namespace SeOne.Domain.Entities;

public class BlogPost
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Excerpt { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string Author { get; set; } = "SE ONE Journal";

    public string ReadTime { get; set; } = "5 min";

    public string Category { get; set; } = "General";

    public string Language { get; set; } = string.Empty;

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}