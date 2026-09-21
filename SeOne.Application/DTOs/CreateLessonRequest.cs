using System.ComponentModel.DataAnnotations;
using SeOne.Application.Validation;

namespace SeOne.Application.DTOs;

public class CreateLessonRequest
{
    [Required]
    [MaxLength(200)]
    [NotEmptyOrWhitespace]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(20000)]
    public string Content { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Order { get; set; }
}
