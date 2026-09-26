using System;
using System.ComponentModel.DataAnnotations;

namespace SeOne.Application.DTOs;

public class WishlistItemDto
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public string CourseTitle { get; set; } = string.Empty;

    public string? CourseImage { get; set; }

    public string CoursePrice { get; set; } = string.Empty;

    public DateTime AddedAt { get; set; }
}

public class AddWishlistItemRequest
{
    [Required]
    public Guid CourseId { get; set; }
}