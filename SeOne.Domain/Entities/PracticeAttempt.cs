namespace SeOne.Domain.Entities;

public class PracticeAttempt
{
    public Guid Id { get; set; }

    public Guid PracticeId { get; set; }

    public Guid StudentId { get; set; }

    public int Score { get; set; }

    public int MaxScore { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public Practice Practice { get; set; } = null!;

    public User Student { get; set; } = null!;

    public ICollection<PracticeAttemptAnswer> Answers { get; set; } = new List<PracticeAttemptAnswer>();
}