using System.ComponentModel.DataAnnotations;

namespace SeOne.Application.DTOs;

public class UpdateTeacherProfileRequest
{
    [MaxLength(500)]
    public string? Avatar { get; set; }

    [MaxLength(100)]
    public string? TeachingLanguage { get; set; }

    [MaxLength(100)]
    public string? Subject { get; set; }

    [MaxLength(50)]
    public string? Level { get; set; }

    [MaxLength(2000)]
    public string? Bio { get; set; }
}
