using System;

namespace SeOne.Domain.Entities;

public class TeacherProfile
{
    public Guid Id { get; set; }

    public Guid TeacherId { get; set; }

    public string? Avatar { get; set; }

    public string? TeachingLanguage { get; set; }

    public string? Subject { get; set; }

    public string? Level { get; set; }

    public string? Bio { get; set; }

    public decimal Rating { get; set; }

    public User Teacher { get; set; } = null!;
}
