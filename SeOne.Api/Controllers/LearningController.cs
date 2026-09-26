using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;
using System.Security.Claims;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/learning")]
[Authorize(Roles = "Student")]
public class LearningController : ApiControllerBase
{
    private readonly SeOneDbContext _db;

    public LearningController(SeOneDbContext db) => _db = db;

    [HttpGet("current")]
    public async Task<IActionResult> Current()
    {
        var studentId = RequireUserId();
        var session = await _db.LearningSessions
            .Include(s => s.Lesson).ThenInclude(l => l.CourseModule)
            .Where(s => s.StudentId == studentId && s.EndedAt == null)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();

        var suggested = await FindNextLessonAsync(studentId, session?.CourseId);
        var today = DateTime.UtcNow.Date;
        var sessions = await _db.LearningSessions
            .Where(s => s.StudentId == studentId && s.StartedAt >= today && s.EndedAt != null)
            .Select(s => new { s.StartedAt, s.EndedAt })
            .ToListAsync();
        var totalStudyTimeToday = sessions.Sum(s => (int)Math.Max(0, (s.EndedAt!.Value - s.StartedAt).TotalMinutes));

        return Ok(new
        {
            activeCourseId = session?.CourseId,
            activeEnrollmentId = session?.EnrollmentId,
            currentLessonId = session?.LessonId,
            currentModuleId = session?.Lesson.CourseModuleId,
            suggestedNextLesson = suggested is null ? null : MapLesson(suggested, await IsCompletedAsync(studentId, suggested.Id)),
            totalStudyTimeToday,
            dailyGoal = 30,
            dailyGoalProgress = Math.Min(100, Math.Round(totalStudyTimeToday * 100d / 30d, 2))
        });
    }

    [HttpPost("start/{courseId:guid}")]
    public async Task<IActionResult> Start(Guid courseId)
    {
        var studentId = RequireUserId();
        var enrollment = await _db.Set<Enrollment>().FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == courseId);
        if (enrollment is null) return Forbid();
        var lesson = await FindNextLessonAsync(studentId, courseId, allowCompletedFallback: true);
        if (lesson is null) return NotFound(new { message = "The course has no lessons." });
        var session = await CreateSessionAsync(studentId, enrollment, lesson);
        return Ok(await MapSessionAsync(session));
    }

    [HttpPost("continue/{lessonId:guid}")]
    public async Task<IActionResult> Continue(Guid lessonId)
    {
        var studentId = RequireUserId();
        var lesson = await _db.Set<Lesson>().Include(l => l.CourseModule).ThenInclude(m => m.Course).FirstOrDefaultAsync(l => l.Id == lessonId);
        if (lesson is null) return NotFound();
        if (!lesson.CourseModule.Course.IsPublished || !await _db.Set<Enrollment>().AnyAsync(e => e.StudentId == studentId && e.CourseId == lesson.CourseModule.CourseId))
            return Forbid();
        var enrollment = await _db.Set<Enrollment>().FirstAsync(e => e.StudentId == studentId && e.CourseId == lesson.CourseModule.CourseId);
        var session = await CreateSessionAsync(studentId, enrollment, lesson);
        return Ok(await MapSessionAsync(session));
    }

    [HttpPost("session/{sessionId:guid}/end")]
    public async Task<IActionResult> End(Guid sessionId)
    {
        var studentId = RequireUserId();
        var session = await _db.LearningSessions.Include(s => s.Lesson).ThenInclude(l => l.CourseModule).FirstOrDefaultAsync(s => s.Id == sessionId && s.StudentId == studentId);
        if (session is null) return NotFound();
        if (session.EndedAt != null) return Ok(await MapSessionSummaryAsync(session));
        var before = await CourseProgressAsync(studentId, session.CourseId);
        session.EndedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var after = await CourseProgressAsync(studentId, session.CourseId);
        return Ok(new
        {
            sessionId = session.Id,
            lessonId = session.LessonId,
            moduleId = session.Lesson.CourseModuleId,
            courseId = session.CourseId,
            duration = (int)Math.Max(0, (session.EndedAt.Value - session.StartedAt).TotalMinutes),
            completed = await IsCompletedAsync(studentId, session.LessonId),
            timeSpent = (int)Math.Max(0, (session.EndedAt.Value - session.StartedAt).TotalMinutes),
            progressBefore = before,
            progressAfter = after
        });
    }

    [HttpGet("recommendations")]
    public async Task<IActionResult> Recommendations()
    {
        var studentId = RequireUserId();
        var result = new List<object>();
        var next = await FindNextLessonAsync(studentId, null);
        if (next is not null)
        {
            result.Add(new { type = "lesson", priority = "high", title = next.Title, description = next.Description, courseId = next.CourseModule.CourseId, lessonId = next.Id, estimatedTime = next.Duration ?? 15, reason = "Continue your current learning path." });
        }
        var practice = await _db.Practices.Where(p => p.IsPublished && _db.Enrollments.Any(e => e.StudentId == studentId && e.CourseId == p.CourseId)).Include(p => p.Course).OrderBy(p => p.CreatedAt).FirstOrDefaultAsync();
        if (practice is not null)
            result.Add(new { type = "practice", priority = "medium", title = practice.Title, description = practice.Description ?? string.Empty, courseId = practice.CourseId, estimatedTime = practice.TimeLimit ?? 10, reason = "Practice reinforces your course material." });
        var review = await _db.Reviews.Where(r => r.StudentId == studentId).OrderByDescending(r => r.UpdatedAt).FirstOrDefaultAsync();
        if (review is not null)
            result.Add(new { type = "review", priority = "low", title = "Review your feedback", description = "Revisit a recent course and continue improving.", courseId = review.CourseId, reason = "Use feedback to guide your next study step." });
        return Ok(result);
    }

    private async Task<LearningSession> CreateSessionAsync(Guid studentId, Enrollment enrollment, Lesson lesson)
    {
        var session = new LearningSession { Id = Guid.NewGuid(), StudentId = studentId, CourseId = enrollment.CourseId, EnrollmentId = enrollment.Id, LessonId = lesson.Id, StartedAt = DateTime.UtcNow };
        _db.LearningSessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    private async Task<object> MapSessionAsync(LearningSession session)
    {
        var lesson = await _db.Lessons.Include(l => l.CourseModule).FirstAsync(l => l.Id == session.LessonId);
        var module = lesson.CourseModule;
        var all = await _db.Lessons.Where(l => l.CourseModuleId == module.Id).OrderBy(l => l.Order).ToListAsync();
        var previous = all.Where(l => l.Order < lesson.Order).OrderByDescending(l => l.Order).FirstOrDefault();
        var next = all.Where(l => l.Order > lesson.Order).OrderBy(l => l.Order).FirstOrDefault();
        if (next is null)
        {
            var nextModule = await _db.Set<CourseModule>().Where(m => m.CourseId == module.CourseId && m.Order > module.Order).OrderBy(m => m.Order).FirstOrDefaultAsync();
            if (nextModule is not null) next = await _db.Lessons.Where(l => l.CourseModuleId == nextModule.Id).OrderBy(l => l.Order).FirstOrDefaultAsync();
        }
        return new { sessionId = session.Id, courseId = session.CourseId, enrollmentId = session.EnrollmentId, lessonId = lesson.Id, moduleId = module.Id, startedAt = session.StartedAt, lesson = MapLesson(lesson, false), module = MapModule(module, all.Count), nextLesson = next is null ? null : MapLesson(next, false), previousLesson = previous is null ? null : MapLesson(previous, false) };
    }

    private async Task<object> MapSessionSummaryAsync(LearningSession session)
    {
        var end = session.EndedAt ?? DateTime.UtcNow;
        return new { sessionId = session.Id, lessonId = session.LessonId, moduleId = session.Lesson.CourseModuleId, courseId = session.CourseId, duration = (int)Math.Max(0, (end - session.StartedAt).TotalMinutes), completed = await IsCompletedAsync(session.StudentId, session.LessonId), timeSpent = (int)Math.Max(0, (end - session.StartedAt).TotalMinutes), progressBefore = await CourseProgressAsync(session.StudentId, session.CourseId), progressAfter = await CourseProgressAsync(session.StudentId, session.CourseId) };
    }

    private async Task<Lesson?> FindNextLessonAsync(Guid studentId, Guid? courseId, bool allowCompletedFallback = false)
    {
        var query = _db.Lessons.Include(l => l.CourseModule).ThenInclude(m => m.Course).Where(l => _db.Enrollments.Any(e => e.StudentId == studentId && e.CourseId == l.CourseModule.CourseId));
        if (courseId.HasValue) query = query.Where(l => l.CourseModule.CourseId == courseId.Value);
        query = query.Where(l => l.CourseModule.Course.IsPublished);
        var lessons = await query.OrderBy(l => l.CourseModule.Order).ThenBy(l => l.Order).ToListAsync();
        foreach (var lesson in lessons)
            if (!await IsCompletedAsync(studentId, lesson.Id)) return lesson;
        return allowCompletedFallback ? lessons.FirstOrDefault() : null;
    }

    private async Task<bool> IsCompletedAsync(Guid studentId, Guid lessonId) => await _db.LessonProgress.AnyAsync(p => p.StudentId == studentId && p.LessonId == lessonId && p.IsCompleted);

    private async Task<double> CourseProgressAsync(Guid studentId, Guid courseId)
    {
        var total = await _db.Lessons.CountAsync(l => l.CourseModule.CourseId == courseId);
        var done = await _db.LessonProgress.CountAsync(p => p.StudentId == studentId && p.IsCompleted && p.Lesson.CourseModule.CourseId == courseId);
        return total == 0 ? 0 : Math.Round(done * 100d / total, 2);
    }

    private static object MapLesson(Lesson l, bool completed) => new { id = l.Id, moduleId = l.CourseModuleId, courseId = l.CourseModule?.CourseId, title = l.Title, description = l.Description, content = l.Content, videoUrl = l.VideoUrl, audioUrl = l.AudioUrl, order = l.Order, duration = l.Duration, isCompleted = completed };
    private static object MapModule(CourseModule m, int lessonCount) => new { id = m.Id, courseId = m.CourseId, title = m.Title, description = m.Description, order = m.Order, lessonCount, completedLessons = 0, isCompleted = false };
}
