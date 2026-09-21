namespace SeOne.Application.DTOs;

public class TeacherDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Avatar { get; set; }

    public string? TeachingLanguage { get; set; }

    public string? Subject { get; set; }

    public string? Level { get; set; }

    public decimal Rating { get; set; }

    public string? Bio { get; set; }
}
