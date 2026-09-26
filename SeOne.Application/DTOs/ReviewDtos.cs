using System;
using System.ComponentModel.DataAnnotations;

namespace SeOne.Application.DTOs;

public class ReviewDto
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? UserAvatar { get; set; }

    public int Rating { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int HelpfulCount { get; set; }

    public bool IsHelpful { get; set; }
}

public class CourseRatingSummaryDto
{
    public Guid CourseId { get; set; }

    public decimal AverageRating { get; set; }

    public int TotalReviews { get; set; }

    public Dictionary<int, int> RatingDistribution { get; set; }
        = new();
}

public class CreateReviewRequest
{
    [Required]
    public Guid CourseId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;
}

public class UpdateReviewRequest
{
    [Range(1, 5)]
    public int? Rating { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(2000)]
    public string? Content { get; set; }
}