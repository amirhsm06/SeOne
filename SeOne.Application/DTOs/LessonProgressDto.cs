namespace SeOne.Application.DTOs;

public class LessonProgressDto
{
    public Guid LessonId { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }
}
