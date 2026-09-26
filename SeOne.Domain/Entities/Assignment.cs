using System;

namespace SeOne.Domain.Entities;

public class Assignment
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public Guid? CourseModuleId { get; set; }

    public Guid TeacherId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Instructions { get; set; }

    public DateTime DueDate { get; set; }

    public decimal MaxPoints { get; set; }

    public List<string> Attachments { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsPublished { get; set; }

    public Course Course { get; set; } = null!;

    public CourseModule? CourseModule { get; set; }

    public User Teacher { get; set; } = null!;

    public ICollection<AssignmentSubmission> Submissions { get; set; }
        = new List<AssignmentSubmission>();
}