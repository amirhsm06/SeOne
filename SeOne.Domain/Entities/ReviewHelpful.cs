using System;

namespace SeOne.Domain.Entities;

public class ReviewHelpful
{
    public Guid Id { get; set; }

    public Guid ReviewId { get; set; }

    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Review Review { get; set; } = null!;

    public User User { get; set; } = null!;
}