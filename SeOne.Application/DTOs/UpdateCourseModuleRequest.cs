using System.ComponentModel.DataAnnotations;
using SeOne.Application.Validation;

namespace SeOne.Application.DTOs;

public class UpdateCourseModuleRequest
{
    [Required]
    [MaxLength(200)]
    [NotEmptyOrWhitespace]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Order { get; set; }
}
