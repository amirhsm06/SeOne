namespace SeOne.Domain.Entities;

public class PracticeAttemptAnswer
{
    public Guid Id { get; set; }

    public Guid AttemptId { get; set; }

    public Guid QuestionId { get; set; }

    public string Answer { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }

    public int PointsEarned { get; set; }

    public PracticeAttempt Attempt { get; set; } = null!;

    public PracticeQuestion Question { get; set; } = null!;
}