namespace SeOne.Domain.Entities;

public class PracticeQuestion
{
    public Guid Id { get; set; }

    public Guid PracticeId { get; set; }

    public string QuestionText { get; set; } = string.Empty;

    public SeOne.Domain.Enums.PracticeQuestionType QuestionType { get; set; }

    public string? OptionsJson { get; set; }

    public string CorrectAnswer { get; set; } = string.Empty;

    public string? Explanation { get; set; }

    public int Points { get; set; } = 1;

    public int Order { get; set; }

    public Practice Practice { get; set; } = null!;
}
