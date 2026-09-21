using System;

namespace SeOne.Domain.Entities;

public class Lesson
{
    public Guid Id { get; set; }

    public Guid CourseModuleId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public int Order { get; set; }

    public DateTime CreatedAt { get; set; }

    public CourseModule CourseModule { get; set; } = null!;
}
