namespace SeOne.Application.DTOs;

public class TeacherDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Avatar { get; set; }

    public string? VideoUrl { get; set; }

    public string? TeachingLanguage { get; set; }

    public string? Subject { get; set; }

    public string? Level { get; set; }

    public decimal Rating { get; set; }

    public string? Bio { get; set; }
}
