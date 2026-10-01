using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class PracticeService : IPracticeService
{
    private readonly SeOneDbContext _context;

    public PracticeService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<List<PracticeContentDto>> GetTeacherPracticeContentAsync(
        Guid teacherId,
        Guid? courseId = null,
        Guid? moduleId = null,
        Guid? lessonId = null,
        string? language = null,
        string? level = null,
        string? category = null)
    {
        var query = BuildPracticeContentQuery();

        query = query.Where(x =>
        _context.Set<CourseInstanceTeacher>()
            .Any(t =>
                t.TeacherId == teacherId &&
                t.CourseInstance.CourseId == x.CourseId));

        query = ApplyFilters(
            query,
            courseId,
            moduleId,
            lessonId,
            language,
            level,
            category);

        var practices = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return practices
            .Select(MapTeacherPractice)
            .ToList();
    }

    public async Task<List<PracticeStudentContentDto>> GetStudentPracticeContentAsync(
        Guid studentId,
        Guid? courseId = null,
        Guid? moduleId = null,
        Guid? lessonId = null,
        string? language = null,
        string? level = null,
        string? category = null)
    {
        var query = BuildPracticeContentQuery()
            .Where(x =>
                x.IsPublished &&
                x.Course.IsPublished &&
                _context.Set<Enrollment>()
                    .Any(e =>
                        e.StudentId == studentId &&
                        e.CourseId == x.CourseId));

        query = ApplyFilters(
            query,
            courseId,
            moduleId,
            lessonId,
            language,
            level,
            category);

        var practices = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return practices
            .Select(MapStudentPractice)
            .ToList();
    }

    public async Task<PracticeContentDto?> GetTeacherPracticeAsync(
        Guid teacherId,
        Guid practiceId)
    {
        var practice = await BuildPracticeContentQuery()
            .Where(x =>
                x.Id == practiceId &&
                _context.Set<CourseInstanceTeacher>()
                    .Any(t =>
                        t.TeacherId == teacherId &&
                        t.CourseInstance.CourseId == x.CourseId))
            .FirstOrDefaultAsync();

        return practice is null
            ? null
            : MapTeacherPractice(practice);
    }

    public async Task<PracticeStudentContentDto?> GetStudentPracticeAsync(
        Guid studentId,
        Guid practiceId)
    {
        var practice = await BuildPracticeContentQuery()
            .Where(x =>
                x.Id == practiceId &&
                x.IsPublished &&
                x.Course.IsPublished &&
                _context.Set<Enrollment>()
                    .Any(e =>
                        e.StudentId == studentId &&
                        e.CourseId == x.CourseId))
            .FirstOrDefaultAsync();

        return practice is null
            ? null
            : MapStudentPractice(practice);
    }

    public async Task<PracticeContentDto?> CreatePracticeAsync(
        Guid teacherId,
        CreatePracticeRequest request)
    {
        if (!ValidatePracticeMetadata(
                request.Title,
                request.Description,
                request.Language,
                request.Level,
                request.Category,
                request.TimeLimit,
                request.PassingScore))
        {
            return null;
        }

        if (!TryBuildQuestions(request.Questions, out var questions))
            return null;

        var course = await _context.Set<Course>()
            .FirstOrDefaultAsync(x =>
                x.Id == request.CourseId &&
                _context.Set<CourseInstanceTeacher>()
                    .Any(t =>
                        t.TeacherId == teacherId &&
                        t.CourseInstance.CourseId == x.Id));

        if (course is null)
            return null;

        if (!await ValidatePlacementAsync(
                request.CourseId,
                request.ModuleId,
                request.LessonId))
        {
            return null;
        }

        var now = DateTime.UtcNow;

        var practice = new Practice
        {
            Id = Guid.NewGuid(),
            CourseId = request.CourseId,
            CourseModuleId = request.ModuleId,
            LessonId = request.LessonId,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
            Language = NormalizeSimple(request.Language),
            Level = request.Level.Trim(),
            Category = request.Category.Trim(),
            TimeLimit = request.TimeLimit,
            PassingScore = request.PassingScore,
            IsPublished = request.IsPublished,
            CreatedAt = now,
            UpdatedAt = now
        };

        practice.Questions = questions
            .Select(x => new PracticeQuestion
            {
                Id = Guid.NewGuid(),
                PracticeId = practice.Id,
                QuestionText = x.QuestionText,
                QuestionType = x.QuestionType,
                OptionsJson = x.OptionsJson,
                CorrectAnswer = x.CorrectAnswer,
                Explanation = x.Explanation,
                Points = x.Points,
                Order = x.Order
            })
            .ToList();

        _context.Set<Practice>().Add(practice);

        await _context.SaveChangesAsync();

        return await GetTeacherPracticeAsync(
            teacherId,
            practice.Id);
    }

    public async Task<PracticeContentDto?> UpdatePracticeAsync(
        Guid teacherId,
        Guid practiceId,
        UpdatePracticeRequest request)
    {
        var practice = await _context.Set<Practice>()
            .Include(x => x.Course)
            .Include(x => x.Questions)
            .FirstOrDefaultAsync(x =>
                x.Id == practiceId &&
                _context.Set<CourseInstanceTeacher>()
                    .Any(t =>
                        t.TeacherId == teacherId &&
                        t.CourseInstance.CourseId == x.CourseId));

        if (practice is null)
            return null;

        if (request.Title is not null &&
            !IsValidText(request.Title, 200))
        {
            return null;
        }

        if (request.Description is not null &&
            request.Description.Length > 5000)
        {
            return null;
        }

        if (request.Language is not null &&
            (!IsValidText(request.Language, 20) ||
             !IsSupportedLanguage(request.Language)))
        {
            return null;
        }

        if (request.Level is not null &&
            !IsValidText(request.Level, 50))
        {
            return null;
        }

        if (request.Category is not null &&
            !IsValidText(request.Category, 100))
        {
            return null;
        }

        if (request.TimeLimit.HasValue &&
            (request.TimeLimit.Value < 1 || request.TimeLimit.Value > 86400))
        {
            return null;
        }

        if (request.PassingScore.HasValue &&
            (request.PassingScore.Value < 0 || request.PassingScore.Value > 100))
        {
            return null;
        }

        List<QuestionDraft> replacementQuestions = [];

        if (request.Questions is not null)
        {
            if (!TryBuildQuestions(request.Questions, out replacementQuestions))
                return null;
        }

        var targetCourseId = request.CourseId ?? practice.CourseId;
        var targetModuleId = request.ModuleId ?? practice.CourseModuleId;
        var targetLessonId = request.LessonId ?? practice.LessonId;

        if (request.CourseId.HasValue)
        {
            var ownsCourse = await _context.Set<Course>()
                .AnyAsync(x =>
                    x.Id == targetCourseId &&
                    _context.Set<CourseInstanceTeacher>()
                        .Any(t =>
                            t.TeacherId == teacherId &&
                            t.CourseInstance.CourseId == x.Id));

            if (!ownsCourse)
                return null;
        }

        if (!await ValidatePlacementAsync(
                targetCourseId,
                targetModuleId,
                targetLessonId))
        {
            return null;
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            practice.CourseId = targetCourseId;
            practice.CourseModuleId = targetModuleId;
            practice.LessonId = targetLessonId;

            if (request.Title is not null)
                practice.Title = request.Title.Trim();

            if (request.Description is not null)
            {
                practice.Description =
                    string.IsNullOrWhiteSpace(request.Description)
                        ? null
                        : request.Description.Trim();
            }

            if (request.Language is not null)
                practice.Language = NormalizeSimple(request.Language);

            if (request.Level is not null)
                practice.Level = request.Level.Trim();

            if (request.Category is not null)
                practice.Category = request.Category.Trim();

            if (request.TimeLimit.HasValue)
                practice.TimeLimit = request.TimeLimit;

            if (request.PassingScore.HasValue)
                practice.PassingScore = request.PassingScore.Value;

            if (request.IsPublished.HasValue)
                practice.IsPublished = request.IsPublished.Value;

            if (request.Questions is not null)
            {
                var hasAttempts = await _context.Set<PracticeAttempt>()
                    .AnyAsync(x => x.PracticeId == practiceId);

                if (hasAttempts)
                {
                    await transaction.RollbackAsync();
                    return null;
                }

                _context.Set<PracticeQuestion>().RemoveRange(practice.Questions);

                var questions = replacementQuestions
                    .Select(x => new PracticeQuestion
                    {
                        Id = Guid.NewGuid(),
                        PracticeId = practice.Id,
                        QuestionText = x.QuestionText,
                        QuestionType = x.QuestionType,
                        OptionsJson = x.OptionsJson,
                        CorrectAnswer = x.CorrectAnswer,
                        Explanation = x.Explanation,
                        Points = x.Points,
                        Order = x.Order
                    })
                    .ToList();

                await _context.Set<PracticeQuestion>()
                    .AddRangeAsync(questions);
            }

            practice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return await GetTeacherPracticeAsync(
            teacherId,
            practiceId);
    }

    public async Task<bool> DeletePracticeAsync(
        Guid teacherId,
        Guid practiceId)
    {
        var ownsPractice = await _context.Set<Practice>()
            .AnyAsync(x =>
                x.Id == practiceId &&
                _context.Set<CourseInstanceTeacher>()
                    .Any(t =>
                        t.TeacherId == teacherId &&
                        t.CourseInstance.CourseId == x.CourseId));

        if (!ownsPractice)
            return false;

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            await _context.Set<PracticeAttemptAnswer>()
                .Where(x => x.Attempt.PracticeId == practiceId)
                .ExecuteDeleteAsync();

            await _context.Set<PracticeAttempt>()
                .Where(x => x.PracticeId == practiceId)
                .ExecuteDeleteAsync();

            await _context.Set<PracticeQuestion>()
                .Where(x => x.PracticeId == practiceId)
                .ExecuteDeleteAsync();

            await _context.Set<Practice>()
                .Where(x => x.Id == practiceId)
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<StartPracticeAttemptDto?> StartAttemptAsync(
        Guid studentId,
        Guid practiceId)
    {
        var practice = await BuildPracticeContentQuery()
            .Where(x =>
                x.Id == practiceId &&
                x.IsPublished &&
                x.Course.IsPublished &&
                _context.Set<Enrollment>()
                    .Any(e =>
                        e.StudentId == studentId &&
                        e.CourseId == x.CourseId))
            .FirstOrDefaultAsync();

        if (practice is null)
            return null;

        var maxScore = practice.Questions.Sum(x => x.Points);

        if (maxScore <= 0)
            return null;

        var attempt = new PracticeAttempt
        {
            Id = Guid.NewGuid(),
            PracticeId = practice.Id,
            StudentId = studentId,
            Score = 0,
            MaxScore = maxScore,
            StartedAt = DateTime.UtcNow
        };

        _context.Set<PracticeAttempt>().Add(attempt);
        await _context.SaveChangesAsync();

        return new StartPracticeAttemptDto
        {
            AttemptId = attempt.Id,
            Practice = MapStudentPractice(practice),
            StartedAt = attempt.StartedAt
        };
    }

    public async Task<PracticeAttemptDto?> SubmitAttemptAsync(
        Guid studentId,
        Guid attemptId,
        SubmitPracticeAttemptRequest request)
    {
        if (request.Answers is null)
            return null;

        if (request.Answers
            .GroupBy(x => x.QuestionId)
            .Any(x => x.Count() > 1))
        {
            return null;
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            var attempt = await _context.Set<PracticeAttempt>()
                .Include(x => x.Practice)
                    .ThenInclude(x => x.Questions)
                .FirstOrDefaultAsync(x =>
                    x.Id == attemptId &&
                    x.StudentId == studentId);

            if (attempt is null || attempt.SubmittedAt.HasValue)
            {
                await transaction.RollbackAsync();
                return null;
            }

            var questions = attempt.Practice.Questions
                .OrderBy(x => x.Order)
                .ToList();

            var questionIds = questions
                .Select(x => x.Id)
                .ToHashSet();

            if (request.Answers.Any(x => !questionIds.Contains(x.QuestionId)))
            {
                await transaction.RollbackAsync();
                return null;
            }

            var answersByQuestion = request.Answers
                .ToDictionary(
                    x => x.QuestionId,
                    x => x.UserAnswer ?? string.Empty);

            _context.Set<PracticeAttemptAnswer>()
                .RemoveRange(
                    await _context.Set<PracticeAttemptAnswer>()
                        .Where(x => x.AttemptId == attemptId)
                        .ToListAsync());

            var totalScore = 0;
            var attemptAnswers = new List<PracticeAttemptAnswer>();

            foreach (var question in questions)
            {
                var userAnswer = answersByQuestion.TryGetValue(
                    question.Id,
                    out var submittedAnswer)
                    ? submittedAnswer.Trim()
                    : string.Empty;

                var isCorrect = IsAnswerCorrect(
                    question,
                    userAnswer);

                var pointsEarned = isCorrect
                    ? question.Points
                    : 0;

                totalScore += pointsEarned;

                attemptAnswers.Add(new PracticeAttemptAnswer
                {
                    Id = Guid.NewGuid(),
                    AttemptId = attempt.Id,
                    QuestionId = question.Id,
                    Answer = userAnswer,
                    IsCorrect = isCorrect,
                    PointsEarned = pointsEarned,
                    Attempt = attempt
                });
            }

            attempt.Score = totalScore;
            attempt.SubmittedAt = DateTime.UtcNow;

            await _context.Set<PracticeAttemptAnswer>()
                .AddRangeAsync(attemptAnswers);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return MapAttempt(attempt);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<PracticeAttemptDto>> GetPracticeAttemptsAsync(
        Guid studentId,
        Guid? practiceId = null)
    {
        var query = _context.Set<PracticeAttempt>()
            .AsNoTracking()
            .Include(x => x.Practice)
            .Include(x => x.Answers)
            .Where(x => x.StudentId == studentId);

        if (practiceId.HasValue)
            query = query.Where(x => x.PracticeId == practiceId.Value);

        var attempts = await query
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync();

        return attempts
            .Select(MapAttempt)
            .ToList();
    }

    public async Task<PracticeAttemptDto?> GetAttemptAsync(
        Guid studentId,
        Guid attemptId)
    {
        var attempt = await _context.Set<PracticeAttempt>()
            .AsNoTracking()
            .Include(x => x.Practice)
            .Include(x => x.Answers)
            .FirstOrDefaultAsync(x =>
                x.Id == attemptId &&
                x.StudentId == studentId);

        return attempt is null
            ? null
            : MapAttempt(attempt);
    }

    public async Task<List<PracticeHistoryDto>> GetHistoryAsync(
        Guid studentId)
    {
        var attempts = await _context.Set<PracticeAttempt>()
            .AsNoTracking()
            .Where(x =>
                x.StudentId == studentId &&
                x.SubmittedAt.HasValue)
            .Select(x => new
            {
                x.Id,
                x.PracticeId,
                PracticeTitle = x.Practice.Title,
                x.Score,
                x.SubmittedAt
            })
            .ToListAsync();

        return attempts
            .GroupBy(x => new
            {
                x.PracticeId,
                x.PracticeTitle
            })
            .Select(group =>
            {
                var best = group
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.SubmittedAt)
                    .First();

                return new PracticeHistoryDto
                {
                    PracticeId = group.Key.PracticeId,
                    PracticeTitle = group.Key.PracticeTitle,
                    TotalAttempts = group.Count(),
                    BestScore = group.Max(x => x.Score),
                    AverageScore = Math.Round(
                        (decimal)group.Average(x => x.Score),
                        2),
                    LastAttemptAt = group.Max(x => x.SubmittedAt),
                    BestAttemptId = best.Id
                };
            })
            .OrderByDescending(x => x.LastAttemptAt)
            .ToList();
    }

    public async Task<StreakInfoDto> GetStreakAsync(
        Guid studentId)
    {
        var completedAt = await _context.Set<PracticeAttempt>()
            .AsNoTracking()
            .Where(x =>
                x.StudentId == studentId &&
                x.SubmittedAt.HasValue)
            .Select(x => x.SubmittedAt!.Value)
            .ToListAsync();

        var grouped = completedAt
            .GroupBy(x => x.Date)
            .OrderBy(x => x.Key)
            .Select(x => new
            {
                Date = x.Key,
                PracticesCompleted = x.Count()
            })
            .ToList();

        if (grouped.Count == 0)
        {
            return new StreakInfoDto();
        }

        var dates = grouped
            .Select(x => x.Date)
            .ToList();

        var longestStreak = 1;
        var runningStreak = 1;

        for (var i = 1; i < dates.Count; i++)
        {
            if (dates[i] == dates[i - 1].AddDays(1))
            {
                runningStreak++;
                longestStreak = Math.Max(
                    longestStreak,
                    runningStreak);
            }
            else
            {
                runningStreak = 1;
            }
        }

        var today = DateTime.UtcNow.Date;
        var latestDate = dates[^1];
        var currentStreak = 0;

        if (latestDate == today || latestDate == today.AddDays(-1))
        {
            currentStreak = 1;

            for (var i = dates.Count - 1; i > 0; i--)
            {
                if (dates[i] == dates[i - 1].AddDays(1))
                    currentStreak++;
                else
                    break;
            }
        }

        return new StreakInfoDto
        {
            CurrentStreak = currentStreak,
            LongestStreak = longestStreak,
            LastPracticeDate = latestDate,
            StreakHistory = grouped
                .Select(x => new StreakHistoryItemDto
                {
                    Date = x.Date,
                    PracticesCompleted = x.PracticesCompleted
                })
                .ToList()
        };
    }

    public async Task<List<PracticeLeaderboardEntryDto>?> GetLeaderboardAsync(
        Guid studentId,
        Guid practiceId)
    {
        var accessible = await _context.Set<Practice>()
            .AnyAsync(x =>
                x.Id == practiceId &&
                x.IsPublished &&
                x.Course.IsPublished &&
                _context.Set<Enrollment>()
                    .Any(e =>
                        e.StudentId == studentId &&
                        e.CourseId == x.CourseId));

        if (!accessible)
            return null;

        var attempts = await _context.Set<PracticeAttempt>()
            .AsNoTracking()
            .Include(x => x.Student)
            .Where(x =>
                x.PracticeId == practiceId &&
                x.SubmittedAt.HasValue)
            .ToListAsync();

        return attempts
            .GroupBy(x => x.StudentId)
            .Select(group => group
                .OrderByDescending(x =>
                    x.MaxScore == 0
                        ? 0m
                        : (decimal)x.Score / x.MaxScore)
                .ThenByDescending(x => x.Score)
                .ThenByDescending(x => x.SubmittedAt)
                .First())
            .OrderByDescending(x =>
                x.MaxScore == 0
                    ? 0m
                    : (decimal)x.Score / x.MaxScore)
            .ThenByDescending(x => x.Score)
            .ThenBy(x => x.SubmittedAt)
            .Take(50)
            .Select(x => new PracticeLeaderboardEntryDto
            {
                UserId = x.StudentId,
                UserName = x.Student.FullName,
                Score = x.Score,
                Percentage = CalculatePercentage(
                    x.Score,
                    x.MaxScore),
                CompletedAt = x.SubmittedAt!.Value
            })
            .ToList();
    }

    private IQueryable<Practice> BuildPracticeContentQuery()
    {
        return _context.Set<Practice>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Course)
            .Include(x => x.CourseModule)
            .Include(x => x.Lesson)
            .Include(x => x.Questions)
            .AsQueryable();
    }

    private static IQueryable<Practice> ApplyFilters(
        IQueryable<Practice> query,
        Guid? courseId,
        Guid? moduleId,
        Guid? lessonId,
        string? language,
        string? level,
        string? category)
    {
        if (courseId.HasValue)
            query = query.Where(x => x.CourseId == courseId.Value);

        if (moduleId.HasValue)
            query = query.Where(x => x.CourseModuleId == moduleId.Value);

        if (lessonId.HasValue)
            query = query.Where(x => x.LessonId == lessonId.Value);

        if (!string.IsNullOrWhiteSpace(language))
        {
            var normalized = language.Trim().ToLowerInvariant();
            query = query.Where(x => x.Language.ToLower() == normalized);
        }

        if (!string.IsNullOrWhiteSpace(level))
        {
            var normalized = level.Trim().ToLowerInvariant();
            query = query.Where(x => x.Level.ToLower() == normalized);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalized = category.Trim().ToLowerInvariant();
            query = query.Where(x => x.Category.ToLower() == normalized);
        }

        return query;
    }

    private async Task<bool> ValidatePlacementAsync(
        Guid courseId,
        Guid? moduleId,
        Guid? lessonId)
    {
        if (moduleId.HasValue)
        {
            var moduleBelongsToCourse = await _context.Set<CourseModule>()
                .AnyAsync(x =>
                    x.Id == moduleId.Value &&
                    x.CourseId == courseId);

            if (!moduleBelongsToCourse)
                return false;
        }

        if (lessonId.HasValue)
        {
            var lesson = await _context.Set<Lesson>()
                .Select(x => new
                {
                    x.Id,
                    x.CourseModuleId,
                    CourseId = x.CourseModule.CourseId
                })
                .FirstOrDefaultAsync(x => x.Id == lessonId.Value);

            if (lesson is null || lesson.CourseId != courseId)
                return false;

            if (moduleId.HasValue &&
                lesson.CourseModuleId != moduleId.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidatePracticeMetadata(
        string? title,
        string? description,
        string? language,
        string? level,
        string? category,
        int? timeLimit,
        int passingScore)
    {
        if (!IsValidText(title, 200) ||
            (description is not null && description.Length > 5000) ||
            !IsValidText(language, 20) ||
            !IsSupportedLanguage(language) ||
            !IsValidText(level, 50) ||
            !IsValidText(category, 100))
        {
            return false;
        }

        if (timeLimit.HasValue &&
            (timeLimit.Value < 1 || timeLimit.Value > 86400))
        {
            return false;
        }

        return passingScore is >= 0 and <= 100;
    }

    private static bool IsValidText(
        string? value,
        int maxLength)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Trim().Length <= maxLength;
    }

    private static bool IsSupportedLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "english" or "german";
    }

    private static bool TryBuildQuestions(
        IEnumerable<CreatePracticeQuestionRequest>? requests,
        out List<QuestionDraft> questions)
    {
        questions = new List<QuestionDraft>();

        if (requests is null)
            return false;

        var items = requests.ToList();

        if (items.Count == 0 || items.Count > 100)
            return false;

        if (items.Select(x => x.Order).Distinct().Count() != items.Count)
            return false;

        var result = new List<QuestionDraft>(items.Count);

        foreach (var request in items)
        {
            if (!IsValidText(request.Question, 5000) ||
                request.Points < 1 ||
                request.Order < 0 ||
                !TryParseQuestionType(request.Type, out var type))
            {
                return false;
            }

            if (request.Options is not null &&
                request.Options.Count > 100)
            {
                return false;
            }

            if (!ValidateQuestionAnswer(
                    type,
                    request.Options,
                    request.CorrectAnswer))
            {
                return false;
            }

            var optionsJson = request.Options is null
                ? null
                : JsonSerializer.Serialize(request.Options
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList());

            if (request.Options is not null &&
                request.Options.Count !=
                request.Options
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count())
            {
                return false;
            }

            result.Add(new QuestionDraft
            {
                QuestionText = request.Question.Trim(),
                QuestionType = type,
                OptionsJson = optionsJson,
                CorrectAnswer = request.CorrectAnswer.GetRawText(),
                Explanation = string.IsNullOrWhiteSpace(request.Explanation)
                    ? null
                    : request.Explanation.Trim(),
                Points = request.Points,
                Order = request.Order
            });
        }

        questions = result
            .OrderBy(x => x.Order)
            .ToList();

        return true;
    }

    private static bool ValidateQuestionAnswer(
        PracticeQuestionType type,
        List<string>? options,
        JsonElement correctAnswer)
    {
        var answers = ExtractStringAnswers(correctAnswer);

        if (answers.Count == 0 ||
            answers.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        if (type == PracticeQuestionType.MultipleChoice)
        {
            if (options is null || options.Count < 2 || answers.Count != 1)
                return false;

            var normalizedOptions = options
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(CanonicalizeAnswer)
                .ToHashSet();

            return normalizedOptions.Contains(
                CanonicalizeAnswer(answers[0]));
        }

        if (type == PracticeQuestionType.Matching)
        {
            return correctAnswer.ValueKind == JsonValueKind.Array &&
                   answers.Count > 0;
        }

        return answers.Count > 0;
    }

    private static List<string> ExtractStringAnswers(
        JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            return value is null
                ? new List<string>()
                : new List<string> { value };
        }

        if (element.ValueKind != JsonValueKind.Array)
            return new List<string>();

        var values = new List<string>();

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                return new List<string>();

            values.Add(item.GetString() ?? string.Empty);
        }

        return values;
    }

    private static bool IsAnswerCorrect(
        PracticeQuestion question,
        string userAnswer)
    {
        if (string.IsNullOrWhiteSpace(userAnswer))
            return false;

        var correctAnswers = ExtractStringAnswersFromJson(
            question.CorrectAnswer);

        if (correctAnswers.Count == 0)
            return false;

        if (question.QuestionType == PracticeQuestionType.Matching)
        {
            if (!TryParseStringArray(userAnswer, out var submittedValues))
                return false;

            var expected = correctAnswers
                .Select(CanonicalizeAnswer)
                .OrderBy(x => x)
                .ToList();

            var actual = submittedValues
                .Select(CanonicalizeAnswer)
                .OrderBy(x => x)
                .ToList();

            return expected.SequenceEqual(actual);
        }

        var normalizedUserAnswer = CanonicalizeAnswer(userAnswer);

        return correctAnswers
            .Any(x => CanonicalizeAnswer(x) == normalizedUserAnswer);
    }

    private static List<string> ExtractStringAnswersFromJson(
        string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return ExtractStringAnswers(document.RootElement);
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static bool TryParseStringArray(
        string value,
        out List<string> result)
    {
        result = new List<string>();

        try
        {
            using var document = JsonDocument.Parse(value);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                    return false;

                result.Add(item.GetString() ?? string.Empty);
            }

            return result.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string CanonicalizeAnswer(string value)
    {
        var normalized = Regex.Replace(
            value.Normalize(),
            @"\s+",
            " ");

        return normalized
            .Trim()
            .Trim('.', ',', '!', '?', ';', ':')
            .ToLowerInvariant();
    }

    private static bool TryParseQuestionType(
        string? value,
        out PracticeQuestionType type)
    {
        type = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value
            .Trim()
            .ToLowerInvariant()
            .Replace('-', '_')
            .Replace(' ', '_');

        switch (normalized)
        {
            case "multiple_choice":
            case "multiplechoice":
                type = PracticeQuestionType.MultipleChoice;
                return true;

            case "fill_blank":
            case "fillblank":
                type = PracticeQuestionType.FillBlank;
                return true;

            case "matching":
                type = PracticeQuestionType.Matching;
                return true;

            case "translation":
                type = PracticeQuestionType.Translation;
                return true;

            case "listening":
                type = PracticeQuestionType.Listening;
                return true;

            default:
                if (int.TryParse(normalized, out var numeric) &&
                    Enum.IsDefined(typeof(PracticeQuestionType), numeric))
                {
                    type = (PracticeQuestionType)numeric;
                    return true;
                }

                return false;
        }
    }

    private static string ToApiQuestionType(
        PracticeQuestionType type)
    {
        return type switch
        {
            PracticeQuestionType.MultipleChoice => "multiple_choice",
            PracticeQuestionType.FillBlank => "fill_blank",
            PracticeQuestionType.Matching => "matching",
            PracticeQuestionType.Translation => "translation",
            PracticeQuestionType.Listening => "listening",
            _ => string.Empty
        };
    }

    private static PracticeContentDto MapTeacherPractice(
        Practice practice)
    {
        return new PracticeContentDto
        {
            Id = practice.Id,
            CourseId = practice.CourseId,
            CourseTitle = practice.Course.Title,
            ModuleId = practice.CourseModuleId,
            ModuleTitle = practice.CourseModule?.Title,
            LessonId = practice.LessonId,
            LessonTitle = practice.Lesson?.Title,
            Title = practice.Title,
            Description = practice.Description ?? string.Empty,
            Language = practice.Language,
            Level = practice.Level,
            Category = practice.Category,
            Questions = practice.Questions
                .OrderBy(x => x.Order)
                .Select(MapTeacherQuestion)
                .ToList(),
            TimeLimit = practice.TimeLimit,
            PassingScore = practice.PassingScore,
            IsPublished = practice.IsPublished,
            CreatedAt = practice.CreatedAt,
            UpdatedAt = practice.UpdatedAt
        };
    }

    private static PracticeStudentContentDto MapStudentPractice(
        Practice practice)
    {
        return new PracticeStudentContentDto
        {
            Id = practice.Id,
            CourseId = practice.CourseId,
            CourseTitle = practice.Course.Title,
            ModuleId = practice.CourseModuleId,
            ModuleTitle = practice.CourseModule?.Title,
            LessonId = practice.LessonId,
            LessonTitle = practice.Lesson?.Title,
            Title = practice.Title,
            Description = practice.Description ?? string.Empty,
            Language = practice.Language,
            Level = practice.Level,
            Category = practice.Category,
            Questions = practice.Questions
                .OrderBy(x => x.Order)
                .Select(MapStudentQuestion)
                .ToList(),
            TimeLimit = practice.TimeLimit,
            PassingScore = practice.PassingScore,
            IsPublished = practice.IsPublished,
            CreatedAt = practice.CreatedAt,
            UpdatedAt = practice.UpdatedAt
        };
    }

    private static PracticeQuestionDto MapTeacherQuestion(
        PracticeQuestion question)
    {
        return new PracticeQuestionDto
        {
            Id = question.Id,
            PracticeId = question.PracticeId,
            Question = question.QuestionText,
            Type = ToApiQuestionType(question.QuestionType),
            Options = DeserializeOptions(question.OptionsJson),
            CorrectAnswer = DeserializeAnswer(question.CorrectAnswer),
            Explanation = question.Explanation,
            Order = question.Order,
            Points = question.Points
        };
    }

    private static PracticeStudentQuestionDto MapStudentQuestion(
        PracticeQuestion question)
    {
        return new PracticeStudentQuestionDto
        {
            Id = question.Id,
            PracticeId = question.PracticeId,
            Question = question.QuestionText,
            Type = ToApiQuestionType(question.QuestionType),
            Options = DeserializeOptions(question.OptionsJson),
            Explanation = question.Explanation,
            Order = question.Order,
            Points = question.Points
        };
    }

    private static List<string>? DeserializeOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static object? DeserializeAnswer(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String)
                return root.GetString();

            if (root.ValueKind == JsonValueKind.Array)
            {
                var values = ExtractStringAnswers(root);
                return values;
            }

            return root.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PracticeAttemptDto MapAttempt(
        PracticeAttempt attempt)
    {
        return new PracticeAttemptDto
        {
            Id = attempt.Id,
            PracticeId = attempt.PracticeId,
            PracticeTitle = attempt.Practice.Title,
            UserId = attempt.StudentId,
            Score = attempt.Score,
            MaxScore = attempt.MaxScore,
            Percentage = CalculatePercentage(
                attempt.Score,
                attempt.MaxScore),
            Passed = attempt.SubmittedAt.HasValue &&
                     CalculatePercentage(
                         attempt.Score,
                         attempt.MaxScore) >= attempt.Practice.PassingScore,
            StartedAt = attempt.StartedAt,
            CompletedAt = attempt.SubmittedAt,
            TimeSpent = CalculateTimeSpent(
                attempt.StartedAt,
                attempt.SubmittedAt),
            Answers = attempt.Answers
                .OrderBy(x => x.QuestionId)
                .Select(x => new PracticeAttemptAnswerDto
                {
                    QuestionId = x.QuestionId,
                    UserAnswer = x.Answer,
                    IsCorrect = x.IsCorrect,
                    PointsEarned = x.PointsEarned
                })
                .ToList()
        };
    }

    private static decimal CalculatePercentage(
        int score,
        int maxScore)
    {
        if (maxScore <= 0)
            return 0m;

        return Math.Round(
            (decimal)score / maxScore * 100m,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static int CalculateTimeSpent(
        DateTime startedAt,
        DateTime? completedAt)
    {
        if (!completedAt.HasValue)
            return 0;

        var seconds =
            (completedAt.Value - startedAt).TotalSeconds;

        return seconds <= 0
            ? 0
            : seconds >= int.MaxValue
                ? int.MaxValue
                : (int)Math.Round(
                    seconds,
                    MidpointRounding.AwayFromZero);
    }

    private static string NormalizeSimple(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private sealed class QuestionDraft
    {
        public string QuestionText { get; init; } = string.Empty;

        public PracticeQuestionType QuestionType { get; init; }

        public string? OptionsJson { get; init; }

        public string CorrectAnswer { get; init; } = string.Empty;

        public string? Explanation { get; init; }

        public int Points { get; init; }

        public int Order { get; init; }
    }
}
