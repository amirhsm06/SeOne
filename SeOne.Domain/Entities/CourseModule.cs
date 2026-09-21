using System;

namespace SeOne.Domain.Entities;

public class CourseModule
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Order { get; set; }

    public DateTime CreatedAt { get; set; }

    public Course Course { get; set; } = null!;
}
