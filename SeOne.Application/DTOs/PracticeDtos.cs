using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SeOne.Application.DTOs;

public class PracticeQuestionDto
{
    public Guid Id { get; set; }

    public Guid PracticeId { get; set; }

    public string Question { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public List<string>? Options { get; set; }

    public object? CorrectAnswer { get; set; }

    public string? Explanation { get; set; }

    public int Order { get; set; }

    public int Points { get; set; }
}

public class PracticeStudentQuestionDto
{
    public Guid Id { get; set; }

    public Guid PracticeId { get; set; }

    public string Question { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public List<string>? Options { get; set; }

    public string? Explanation { get; set; }

    public int Order { get; set; }

    public int Points { get; set; }
}

public class PracticeContentDto
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public string CourseTitle { get; set; } = string.Empty;

    public Guid? ModuleId { get; set; }

    public string? ModuleTitle { get; set; }

    public Guid? LessonId { get; set; }

    public string? LessonTitle { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public List<PracticeQuestionDto> Questions { get; set; } = new();

    public int? TimeLimit { get; set; }

    public int PassingScore { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class PracticeStudentContentDto
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public string CourseTitle { get; set; } = string.Empty;

    public Guid? ModuleId { get; set; }

    public string? ModuleTitle { get; set; }

    public Guid? LessonId { get; set; }

    public string? LessonTitle { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public List<PracticeStudentQuestionDto> Questions { get; set; } = new();

    public int? TimeLimit { get; set; }

    public int PassingScore { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class CreatePracticeRequest
{
    [Required]
    public Guid CourseId { get; set; }

    public Guid? ModuleId { get; set; }

    public Guid? LessonId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(20)]
    public string Language { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Level { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [Range(1, 86400)]
    public int? TimeLimit { get; set; }

    [Range(0, 100)]
    public int PassingScore { get; set; }

    public bool IsPublished { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public List<CreatePracticeQuestionRequest> Questions { get; set; } = new();
}

public class UpdatePracticeRequest
{
    public Guid? CourseId { get; set; }

    public Guid? ModuleId { get; set; }

    public Guid? LessonId { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(5000)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string? Language { get; set; }

    [MaxLength(50)]
    public string? Level { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [Range(1, 86400)]
    public int? TimeLimit { get; set; }

    [Range(0, 100)]
    public int? PassingScore { get; set; }

    public bool? IsPublished { get; set; }

    [MinLength(1)]
    [MaxLength(100)]
    public List<CreatePracticeQuestionRequest>? Questions { get; set; }
}

public class CreatePracticeQuestionRequest
{
    [Required]
    [MaxLength(5000)]
    public string Question { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = string.Empty;

    public List<string>? Options { get; set; }

    [Required]
    public JsonElement CorrectAnswer { get; set; }

    [MaxLength(5000)]
    public string? Explanation { get; set; }

    [Range(1, 1000000)]
    public int Points { get; set; } = 1;

    [Range(0, 1000000)]
    public int Order { get; set; }
}

public class StartPracticeAttemptRequest
{
    [Required]
    public Guid PracticeId { get; set; }
}

public class StartPracticeAttemptDto
{
    public Guid AttemptId { get; set; }

    public PracticeStudentContentDto Practice { get; set; } = null!;

    public DateTime StartedAt { get; set; }
}

public class SubmitPracticeAttemptRequest
{
    [Required]
    public List<SubmitPracticeAnswerRequest> Answers { get; set; } = new();
}

public class SubmitPracticeAnswerRequest
{
    [Required]
    public Guid QuestionId { get; set; }

    [MaxLength(10000)]
    public string UserAnswer { get; set; } = string.Empty;
}

public class PracticeAttemptAnswerDto
{
    public Guid QuestionId { get; set; }

    public string UserAnswer { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }

    public int PointsEarned { get; set; }
}

public class PracticeAttemptDto
{
    public Guid Id { get; set; }

    public Guid PracticeId { get; set; }

    public string PracticeTitle { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public int Score { get; set; }

    public int MaxScore { get; set; }

    public decimal Percentage { get; set; }

    public bool Passed { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int TimeSpent { get; set; }

    public List<PracticeAttemptAnswerDto> Answers { get; set; } = new();
}

public class PracticeHistoryDto
{
    public Guid PracticeId { get; set; }

    public string PracticeTitle { get; set; } = string.Empty;

    public int TotalAttempts { get; set; }

    public int BestScore { get; set; }

    public decimal AverageScore { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public Guid? BestAttemptId { get; set; }
}

public class StreakHistoryItemDto
{
    public DateTime Date { get; set; }

    public int PracticesCompleted { get; set; }
}

public class StreakInfoDto
{
    public int CurrentStreak { get; set; }

    public int LongestStreak { get; set; }

    public DateTime? LastPracticeDate { get; set; }

    public List<StreakHistoryItemDto> StreakHistory { get; set; } = new();
}

public class PracticeLeaderboardEntryDto
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public int Score { get; set; }

    public decimal Percentage { get; set; }

    public DateTime CompletedAt { get; set; }
}
