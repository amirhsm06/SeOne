using System;

namespace SeOne.Domain.Entities;

public class WishlistItem
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid CourseId { get; set; }

    public DateTime AddedAt { get; set; }

    public User Student { get; set; } = null!;

    public Course Course { get; set; } = null!;
}