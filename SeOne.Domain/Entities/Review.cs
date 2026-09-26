using System;

namespace SeOne.Domain.Entities;

public class Review
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public Guid StudentId { get; set; }

    public int Rating { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Course Course { get; set; } = null!;

    public User Student { get; set; } = null!;

    public ICollection<ReviewHelpful> HelpfulMarks { get; set; }
        = new List<ReviewHelpful>();
}