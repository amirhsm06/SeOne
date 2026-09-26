namespace SeOne.Domain.Entities;

public class LessonProgress
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid LessonId { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int TimeSpent { get; set; }

    public int LastPosition { get; set; }

    public DateTime? LastAccessedAt { get; set; }

    public User Student { get; set; } = null!;

    public Lesson Lesson { get; set; } = null!;
}
