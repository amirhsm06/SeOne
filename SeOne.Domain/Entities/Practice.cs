using SeOne.Domain.Enums;

namespace SeOne.Domain.Entities;

public class Practice
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public Guid? CourseModuleId { get; set; }

    public Guid? LessonId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Language { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public int? TimeLimit { get; set; }

    public int PassingScore { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Course Course { get; set; } = null!;

    public CourseModule? CourseModule { get; set; }

    public Lesson? Lesson { get; set; }

    public ICollection<PracticeQuestion> Questions { get; set; } = new List<PracticeQuestion>();

    public ICollection<PracticeAttempt> Attempts { get; set; } = new List<PracticeAttempt>();
}
