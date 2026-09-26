using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api")]
public class CompatibilityController : ApiControllerBase
{
    private readonly SeOneDbContext _db;
    private readonly IEnrollmentService _enrollmentService;
    private readonly IProgressService _progressService;
    private readonly ITeacherAvailabilityService _availabilityService;
    private readonly IBlogService _blogService;

    public CompatibilityController(
        SeOneDbContext db,
        IEnrollmentService enrollmentService,
        IProgressService progressService,
        ITeacherAvailabilityService availabilityService,
        IBlogService blogService)
    {
        _db = db;
        _enrollmentService = enrollmentService;
        _progressService = progressService;
        _availabilityService = availabilityService;
        _blogService = blogService;
    }

    // -----------------------------------------------------------------
    // Enrollment aliases used by the frontend.
    // -----------------------------------------------------------------

    [HttpPost("enrollment/{courseId:guid}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Enroll(Guid courseId)
    {
        var studentId = RequireUserId();
        var course = await _db.Set<Course>().FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null || !course.IsPublished)
            return NotFound(new { message = "Course not found." });

        if (course.Price > 0)
        {
            return Ok(new
            {
                paymentRequired = true,
                paymentUrl = (string?)null,
                courseId,
                amount = course.DiscountPercent > 0
                    ? Math.Round(course.Price * (1m - course.DiscountPercent / 100m), 2)
                    : course.Price,
                currency = course.Currency
            });
        }

        var enrollment = await _enrollmentService.EnrollAsync(studentId, courseId);
        if (enrollment is null)
            return Conflict(new { message = "You are already enrolled or enrollment is unavailable." });

        return Ok(new { enrollment, paymentRequired = false });
    }

    [HttpGet("enrollment/user")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> MyEnrollments()
    {
        var studentId = RequireUserId();
        var items = await _db.Set<Enrollment>()
            .Where(e => e.StudentId == studentId)
            .Include(e => e.Course)
            .OrderByDescending(e => e.EnrolledAt)
            .Select(e => new
            {
                id = e.Id,
                courseId = e.CourseId,
                userId = e.StudentId,
                enrolledAt = e.EnrolledAt,
                status = "active",
                progress = 0d
            })
            .ToListAsync();

        var courseIds = items.Select(x => x.courseId).ToList();
        if (courseIds.Count > 0)
        {
            var completed = await _db.Set<LessonProgress>()
                .Where(p => p.StudentId == studentId && p.IsCompleted && courseIds.Contains(p.Lesson.CourseModule.CourseId))
                .GroupBy(p => p.Lesson.CourseModule.CourseId)
                .Select(g => new { CourseId = g.Key, Count = g.Count() })
                .ToListAsync();

            var totals = await _db.Set<Lesson>()
                .Where(l => courseIds.Contains(l.CourseModule.CourseId))
                .GroupBy(l => l.CourseModule.CourseId)
                .Select(g => new { CourseId = g.Key, Count = g.Count() })
                .ToListAsync();

            var completedLookup = completed.ToDictionary(x => x.CourseId, x => x.Count);
            var totalLookup = totals.ToDictionary(x => x.CourseId, x => x.Count);
            items = items.Select(x =>
            {
                var total = totalLookup.GetValueOrDefault(x.courseId);
                var done = completedLookup.GetValueOrDefault(x.courseId);
                var progress = total == 0 ? 0d : Math.Round(done * 100d / total, 2);
                return new
                {
                    x.id,
                    x.courseId,
                    x.userId,
                    x.enrolledAt,
                    x.status,
                    progress
                };
            }).ToList();
        }

        return Ok(items);
    }

    [HttpGet("enrollment/{enrollmentId:guid}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetEnrollment(Guid enrollmentId)
    {
        var studentId = RequireUserId();
        var enrollment = await _db.Set<Enrollment>()
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.StudentId == studentId);

        if (enrollment is null)
            return NotFound();

        var progress = await BuildCourseProgressAsync(studentId, enrollment.CourseId);
        return Ok(new
        {
            id = enrollment.Id,
            courseId = enrollment.CourseId,
            userId = enrollment.StudentId,
            enrolledAt = enrollment.EnrolledAt,
            status = "active",
            progress = progress.OverallProgress
        });
    }

    [HttpDelete("enrollment/{enrollmentId:guid}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CancelEnrollment(Guid enrollmentId)
    {
        var studentId = RequireUserId();
        var enrollment = await _db.Set<Enrollment>()
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.StudentId == studentId);

        if (enrollment is null)
            return NotFound();

        await _db.Set<LearningSession>()
            .Where(x => x.EnrollmentId == enrollment.Id)
            .ExecuteDeleteAsync();

        _db.Set<Enrollment>().Remove(enrollment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("enrollment/{enrollmentId:guid}/progress")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> EnrollmentProgress(Guid enrollmentId)
    {
        var studentId = RequireUserId();
        var enrollment = await _db.Set<Enrollment>()
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.StudentId == studentId);

        if (enrollment is null)
            return NotFound();

        var progress = await BuildCourseProgressAsync(studentId, enrollment.CourseId);
        return Ok(new
        {
            enrollmentId,
            courseId = enrollment.CourseId,
            progress.TotalModulesCompleted,
            totalModules = progress.TotalModules,
            completedModules = progress.TotalModulesCompleted,
            completedLessons = progress.CompletedLessons,
            totalLessons = progress.TotalLessons,
            overallProgress = progress.OverallProgress,
            lastAccessedAt = progress.LastAccessedAt
        });
    }

    // -----------------------------------------------------------------
    // Module aliases.
    // -----------------------------------------------------------------

    [HttpGet("modules/{moduleId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetModule(Guid moduleId)
    {
        var userId = RequireUserId();
        var userRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        var module = await _db.Set<CourseModule>()
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == moduleId);

        if (module is null)
            return NotFound();

        if (userRole.Equals("Teacher", StringComparison.OrdinalIgnoreCase) && module.Course.TeacherId != userId)
            return Forbid();

        if (userRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
        {
            var enrolled = await _db.Set<Enrollment>().AnyAsync(e => e.StudentId == userId && e.CourseId == module.CourseId);
            if (!module.Course.IsPublished || !enrolled)
                return Forbid();
        }

        var lessons = await _db.Set<Lesson>()
            .Where(l => l.CourseModuleId == moduleId)
            .OrderBy(l => l.Order)
            .ToListAsync();

        var result = await BuildModuleResponseAsync(module, userId, userRole, lessons);
        return Ok(result);
    }

    [HttpGet("modules/{moduleId:guid}/lessons")]
    [Authorize]
    public async Task<IActionResult> GetModuleLessons(Guid moduleId)
    {
        var userId = RequireUserId();
        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var module = await _db.Set<CourseModule>()
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == moduleId);

        if (module is null)
            return NotFound();

        if (role.Equals("Teacher", StringComparison.OrdinalIgnoreCase) && module.Course.TeacherId != userId)
            return Forbid();

        if (role.Equals("Student", StringComparison.OrdinalIgnoreCase))
        {
            if (!module.Course.IsPublished || !await _db.Set<Enrollment>().AnyAsync(e => e.StudentId == userId && e.CourseId == module.CourseId))
                return Forbid();
        }

        var lessons = await _db.Set<Lesson>()
            .Where(l => l.CourseModuleId == moduleId)
            .OrderBy(l => l.Order)
            .ToListAsync();

        var lessonIds = lessons.Select(l => l.Id).ToList();
        var completedIds = await _db.Set<LessonProgress>()
            .Where(p => p.StudentId == userId && p.IsCompleted && lessonIds.Contains(p.LessonId))
            .Select(p => p.LessonId)
            .ToListAsync();

        return Ok(lessons.Select(l => MapLesson(l, completedIds.Contains(l.Id), module.CourseId)));
    }

    // -----------------------------------------------------------------
    // Lesson aliases and progress.
    // -----------------------------------------------------------------

    [HttpGet("lessons/{lessonId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetLesson(Guid lessonId)
    {
        var userId = RequireUserId();
        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var lesson = await _db.Set<Lesson>()
            .Include(l => l.CourseModule)
                .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.Id == lessonId);

        if (lesson is null)
            return NotFound();

        if (role.Equals("Teacher", StringComparison.OrdinalIgnoreCase) && lesson.CourseModule.Course.TeacherId != userId)
            return Forbid();

        if (role.Equals("Student", StringComparison.OrdinalIgnoreCase))
        {
            if (!lesson.CourseModule.Course.IsPublished || !await _db.Set<Enrollment>().AnyAsync(e => e.StudentId == userId && e.CourseId == lesson.CourseModule.CourseId))
                return Forbid();
        }

        var progress = await _db.Set<LessonProgress>().FirstOrDefaultAsync(p => p.StudentId == userId && p.LessonId == lessonId);
        return Ok(MapLesson(lesson, progress?.IsCompleted ?? false, lesson.CourseModule.CourseId));
    }

    [HttpPost("lessons/{lessonId:guid}/complete")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CompleteLesson(Guid lessonId)
    {
        var studentId = RequireUserId();
        var progress = await _progressService.UpdateLessonProgressAsync(studentId, lessonId, true);
        if (progress is null)
            return NotFound(new { message = "Lesson is not available for this student." });

        var entity = await _db.Set<LessonProgress>().FirstOrDefaultAsync(p => p.StudentId == studentId && p.LessonId == lessonId);
        if (entity is not null)
        {
            entity.LastAccessedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return Ok(new
        {
            lessonId = progress.LessonId,
            moduleId = entity is null ? null : (Guid?)await _db.Set<Lesson>().Where(l => l.Id == lessonId).Select(l => l.CourseModuleId).FirstOrDefaultAsync(),
            isCompleted = progress.IsCompleted,
            completedAt = progress.CompletedAt,
            timeSpent = entity?.TimeSpent ?? 0,
            lastPosition = entity?.LastPosition ?? 0
        });
    }

    [HttpGet("lessons/{lessonId:guid}/progress")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetLessonProgress(Guid lessonId)
    {
        var studentId = RequireUserId();
        var lesson = await _db.Set<Lesson>().FirstOrDefaultAsync(l => l.Id == lessonId);
        if (lesson is null)
            return NotFound();

        var courseId = await _db.Set<CourseModule>()
            .Where(m => m.Id == lesson.CourseModuleId)
            .Select(m => m.CourseId)
            .FirstAsync();

        if (!await _db.Set<Enrollment>().AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId))
            return Forbid();

        var progress = await _db.Set<LessonProgress>().FirstOrDefaultAsync(p => p.StudentId == studentId && p.LessonId == lessonId);
        return Ok(new
        {
            lessonId,
            moduleId = lesson.CourseModuleId,
            isCompleted = progress?.IsCompleted ?? false,
            completedAt = progress?.CompletedAt,
            timeSpent = progress?.TimeSpent ?? 0,
            lastPosition = progress?.LastPosition ?? 0
        });
    }

    [HttpPatch("lessons/{lessonId:guid}/progress")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> UpdateLessonProgress(Guid lessonId, [FromBody] UpdateLessonProgressCompatibilityRequest request)
    {
        var studentId = RequireUserId();
        if (request.TimeSpent is < 0 or > 2_000_000 || request.LastPosition is < 0 or > 2_000_000)
            return BadRequest(new { message = "Invalid progress values." });

        var lesson = await _db.Set<Lesson>()
            .Include(l => l.CourseModule)
            .FirstOrDefaultAsync(l => l.Id == lessonId);

        if (lesson is null || !await _db.Set<Enrollment>().AnyAsync(e => e.StudentId == studentId && e.CourseId == lesson.CourseModule.CourseId))
            return NotFound();

        var progress = await _db.Set<LessonProgress>().FirstOrDefaultAsync(p => p.StudentId == studentId && p.LessonId == lessonId);
        if (progress is null)
        {
            progress = new LessonProgress
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                LessonId = lessonId,
                IsCompleted = false
            };
            _db.Set<LessonProgress>().Add(progress);
        }

        if (request.TimeSpent.HasValue)
            progress.TimeSpent = request.TimeSpent.Value;
        if (request.LastPosition.HasValue)
            progress.LastPosition = request.LastPosition.Value;
        progress.LastAccessedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            lessonId,
            moduleId = lesson.CourseModuleId,
            isCompleted = progress.IsCompleted,
            completedAt = progress.CompletedAt,
            timeSpent = progress.TimeSpent,
            lastPosition = progress.LastPosition
        });
    }

    // -----------------------------------------------------------------
    // Progress aliases / achievements.
    // -----------------------------------------------------------------

    [HttpGet("progress/user")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> UserProgress()
    {
        var studentId = RequireUserId();
        var enrollments = await _db.Set<Enrollment>()
            .Where(e => e.StudentId == studentId)
            .ToListAsync();

        var courseIds = enrollments.Select(e => e.CourseId).ToList();
        var totalCourses = courseIds.Count;
        var lessonTotals = totalCourses == 0
            ? 0
            : await _db.Set<Lesson>().CountAsync(l => courseIds.Contains(l.CourseModule.CourseId));
        var completedLessons = await _db.Set<LessonProgress>()
            .Where(p => p.StudentId == studentId && p.IsCompleted && courseIds.Contains(p.Lesson.CourseModule.CourseId))
            .CountAsync();

        var courseProgress = new List<object>();
        foreach (var enrollment in enrollments.OrderByDescending(e => e.EnrolledAt).Take(5))
        {
            var progress = await BuildCourseProgressAsync(studentId, enrollment.CourseId);
            courseProgress.Add(new
            {
                courseId = enrollment.CourseId,
                courseTitle = await _db.Set<Course>().Where(c => c.Id == enrollment.CourseId).Select(c => c.Title).FirstOrDefaultAsync() ?? string.Empty,
                enrollmentId = enrollment.Id,
                progress = progress.OverallProgress,
                lastAccessedAt = progress.LastAccessedAt ?? enrollment.EnrolledAt,
                completedLessons = progress.CompletedLessons,
                totalLessons = progress.TotalLessons
            });
        }

        var today = DateTime.UtcNow.Date;
        var studyTime = await _db.Set<LessonProgress>()
            .Where(p => p.StudentId == studentId)
            .SumAsync(p => (long?)p.TimeSpent) ?? 0;
        var todayStudyTime = await _db.Set<LearningSession>()
            .Where(s => s.StudentId == studentId && s.StartedAt >= today && s.EndedAt != null)
            .Select(s => new { s.StartedAt, s.EndedAt })
            .ToListAsync();
        var todayMinutes = todayStudyTime.Sum(x => (int)Math.Max(0, (x.EndedAt!.Value - x.StartedAt).TotalMinutes));

        var completedCourses = 0;
        foreach (var courseId in courseIds)
        {
            var p = await BuildCourseProgressAsync(studentId, courseId);
            if (p.TotalLessons > 0 && p.CompletedLessons == p.TotalLessons)
                completedCourses++;
        }

        var currentStreak = await CalculateCurrentStreakAsync(studentId);
        var longestStreak = await CalculateLongestStreakAsync(studentId);

        var weekly = new List<int>();
        for (var i = 6; i >= 0; i--)
        {
            var dayStart = today.AddDays(-i);
            var dayEnd = dayStart.AddDays(1);
            var minutes = await _db.Set<LearningSession>()
                .Where(s => s.StudentId == studentId && s.StartedAt >= dayStart && s.StartedAt < dayEnd && s.EndedAt != null)
                .Select(s => new { s.StartedAt, s.EndedAt })
                .ToListAsync();
            weekly.Add(minutes.Sum(x => (int)Math.Max(0, (x.EndedAt!.Value - x.StartedAt).TotalMinutes)));
        }

        return Ok(new
        {
            userId = studentId,
            totalCoursesEnrolled = totalCourses,
            totalCoursesCompleted = completedCourses,
            totalLessonsCompleted = completedLessons,
            totalStudyTime = (int)Math.Min(int.MaxValue, studyTime),
            currentStreak,
            longestStreak,
            weeklyStudyTime = weekly,
            recentlyActiveCourses = courseProgress
        });
    }

    [HttpGet("progress/course/{courseId:guid}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CourseProgress(Guid courseId)
    {
        var studentId = RequireUserId();
        var enrollment = await _db.Set<Enrollment>().AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId);
        if (!enrollment)
            return Forbid();

        var progress = await BuildCourseProgressAsync(studentId, courseId);
        return Ok(new
        {
            courseId,
            courseTitle = await _db.Set<Course>().Where(c => c.Id == courseId).Select(c => c.Title).FirstOrDefaultAsync() ?? string.Empty,
            enrollmentId = await _db.Set<Enrollment>().Where(e => e.StudentId == studentId && e.CourseId == courseId).Select(e => e.Id).FirstAsync(),
            progress = progress.OverallProgress,
            lastAccessedAt = progress.LastAccessedAt,
            completedLessons = progress.CompletedLessons,
            totalLessons = progress.TotalLessons
        });
    }

    [HttpGet("progress/achievements")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Achievements()
    {
        var studentId = RequireUserId();
        return Ok((await BuildAchievementsAsync(studentId)).Select(x => x.ToResponse()));
    }

    [HttpPost("progress/achievements/{achievementId}/claim")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> ClaimAchievement(string achievementId)
    {
        var studentId = RequireUserId();
        var achievements = await BuildAchievementsAsync(studentId);
        var achievement = achievements.FirstOrDefault(x => x.id == achievementId);
        if (achievement is null)
            return NotFound();
        if (!achievement.isUnlocked)
            return BadRequest(new { message = "Achievement is not unlocked yet." });
        return Ok(achievement.ToResponse());
    }

    // -----------------------------------------------------------------
    // Teacher availability alias.
    // -----------------------------------------------------------------

    [HttpGet("teacher/availability")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> MyAvailability()
    {
        var teacherId = RequireUserId();
        var slots = await _availabilityService.GetMyAvailabilityAsync(teacherId);
        return Ok(slots.Select(MapAvailability));
    }

    [HttpPost("teacher/availability")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> CreateAvailability([FromBody] CreateAvailabilityCompatibilityRequest request)
    {
        var teacherId = RequireUserId();
        if (!TryParseDay(request.Day, out var day))
            return BadRequest(new { message = "Use a valid day and time range such as Monday and 09:00-10:00." });

        if (!TryParseRange(request.Time, out var startTime, out var end))
            return BadRequest(new { message = "Time must be formatted as HH:mm-HH:mm." });

        var created = await _availabilityService.CreateAsync(teacherId, day, startTime, end, true);
        if (created is null)
            return Conflict(new { message = "Availability slot is invalid or already exists." });

        return Ok(MapAvailability(created));
    }

    [HttpDelete("teacher/availability/{slotId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> DeleteAvailability(Guid slotId)
    {
        var teacherId = RequireUserId();
        var ok = await _availabilityService.DeleteAsync(slotId, teacherId);
        return ok ? NoContent() : NotFound();
    }

    // -----------------------------------------------------------------
    // Cambridge blog endpoint expected by the frontend.
    // -----------------------------------------------------------------

    [HttpGet("blog/cambridge")]
    [AllowAnonymous]
    public async Task<IActionResult> CambridgeBlog()
    {
        var posts = await _blogService.GetPublishedPostsAsync("en");
        return Ok(new
        {
            posts = posts.Select(p => new
            {
                p.Id,
                p.Title,
                p.Excerpt,
                p.Author,
                date = p.CreatedAt,
                p.ReadTime,
                p.Category,
                image = p.ImageUrl,
                source = "cambridge"
            })
        });
    }

    private async Task<object> BuildModuleResponseAsync(CourseModule module, Guid userId, string role, List<Lesson> lessons)
    {
        var completedIds = await _db.Set<LessonProgress>()
            .Where(p => p.StudentId == userId && p.IsCompleted && lessons.Select(l => l.Id).Contains(p.LessonId))
            .Select(p => p.LessonId)
            .ToListAsync();

        return new
        {
            id = module.Id,
            courseId = module.CourseId,
            title = module.Title,
            description = module.Description,
            order = module.Order,
            lessonCount = lessons.Count,
            completedLessons = completedIds.Count,
            isCompleted = lessons.Count > 0 && completedIds.Count == lessons.Count,
            lessons = lessons.Select(l => MapLesson(l, completedIds.Contains(l.Id))).ToList()
        };
    }

    private static object MapLesson(Lesson lesson, bool completed, Guid? courseId = null)
    {
        return new
        {
            id = lesson.Id,
            moduleId = lesson.CourseModuleId,
            courseId = courseId ?? lesson.CourseModule?.CourseId,
            title = lesson.Title,
            description = lesson.Description,
            content = lesson.Content,
            videoUrl = lesson.VideoUrl,
            audioUrl = lesson.AudioUrl,
            order = lesson.Order,
            duration = lesson.Duration,
            isCompleted = completed
        };
    }

    private static object MapAvailability(TeacherAvailabilityDto slot) => new
    {
        id = slot.Id,
        teacherId = slot.TeacherId,
        day = slot.DayOfWeek.ToString(),
        dayOfWeek = slot.DayOfWeek.ToString(),
        time = $"{slot.StartTime:hh\\:mm}-{slot.EndTime:hh\\:mm}",
        startTime = slot.StartTime.ToString(@"hh\:mm"),
        endTime = slot.EndTime.ToString(@"hh\:mm"),
        booked = false,
        isBooked = false,
        isAvailable = slot.IsAvailable
    };

    private async Task<CourseProgressSummaryInternal> BuildCourseProgressAsync(Guid studentId, Guid courseId)
    {
        var lessons = await _db.Set<Lesson>()
            .Where(l => l.CourseModule.CourseId == courseId)
            .Select(l => l.Id)
            .ToListAsync();

        var moduleIds = await _db.Set<CourseModule>()
            .Where(m => m.CourseId == courseId)
            .Select(m => m.Id)
            .ToListAsync();

        var done = lessons.Count == 0
            ? []
            : await _db.Set<LessonProgress>()
                .Where(p => p.StudentId == studentId && p.IsCompleted && lessons.Contains(p.LessonId))
                .Select(p => p.LessonId)
                .ToListAsync();

        var last = await _db.Set<LessonProgress>()
            .Where(p => p.StudentId == studentId && lessons.Contains(p.LessonId) && p.LastAccessedAt != null)
            .OrderByDescending(p => p.LastAccessedAt)
            .Select(p => p.LastAccessedAt)
            .FirstOrDefaultAsync();

        var doneModules = moduleIds.Count == 0
            ? 0
            : await _db.Set<CourseModule>()
                .Where(m => m.CourseId == courseId)
                .Select(m => new
                {
                    ModuleId = m.Id,
                    Total = _db.Set<Lesson>().Count(l => l.CourseModuleId == m.Id),
                    Done = _db.Set<LessonProgress>().Count(p => p.StudentId == studentId && p.IsCompleted && p.Lesson.CourseModuleId == m.Id)
                })
                .CountAsync(x => x.Total > 0 && x.Done == x.Total);

        return new CourseProgressSummaryInternal
        {
            TotalLessons = lessons.Count,
            CompletedLessons = done.Count,
            TotalModules = moduleIds.Count,
            TotalModulesCompleted = doneModules,
            OverallProgress = lessons.Count == 0 ? 0 : Math.Round(done.Count * 100d / lessons.Count, 2),
            LastAccessedAt = last
        };
    }

    private async Task<List<AchievementInternal>> BuildAchievementsAsync(Guid studentId)
    {
        var completedLessons = await _db.Set<LessonProgress>().CountAsync(p => p.StudentId == studentId && p.IsCompleted);
        var attempts = await _db.Set<PracticeAttempt>().CountAsync(a => a.StudentId == studentId && a.SubmittedAt != null);
        var enrollments = await _db.Set<Enrollment>().CountAsync(e => e.StudentId == studentId);
        var currentStreak = await CalculateCurrentStreakAsync(studentId);
        var courseCompletions = 0;

        foreach (var courseId in await _db.Set<Enrollment>().Where(e => e.StudentId == studentId).Select(e => e.CourseId).ToListAsync())
        {
            var p = await BuildCourseProgressAsync(studentId, courseId);
            if (p.TotalLessons > 0 && p.CompletedLessons == p.TotalLessons)
                courseCompletions++;
        }

        return
        [
            new AchievementInternal("first-lesson", "First Lesson", "Complete your first lesson.", "lesson", completedLessons >= 1, Math.Min(completedLessons, 1), 1),
            new AchievementInternal("ten-lessons", "Ten Lessons", "Complete ten lessons.", "book", completedLessons >= 10, Math.Min(completedLessons, 10), 10),
            new AchievementInternal("first-practice", "First Practice", "Submit your first practice attempt.", "practice", attempts >= 1, Math.Min(attempts, 1), 1),
            new AchievementInternal("three-courses", "Three Courses", "Enroll in three courses.", "courses", enrollments >= 3, Math.Min(enrollments, 3), 3),
            new AchievementInternal("week-streak", "Week Streak", "Study on seven consecutive days.", "streak", currentStreak >= 7, Math.Min(currentStreak, 7), 7),
            new AchievementInternal("course-complete", "Course Complete", "Finish a course.", "trophy", courseCompletions >= 1, Math.Min(courseCompletions, 1), 1)
        ];
    }

    private async Task<int> CalculateCurrentStreakAsync(Guid studentId)
    {
        var dates = await StudyDatesAsync(studentId);
        if (dates.Count == 0)
            return 0;

        var today = DateTime.UtcNow.Date;
        var cursor = dates.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (dates.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }
        return streak;
    }

    private async Task<int> CalculateLongestStreakAsync(Guid studentId)
    {
        var dates = (await StudyDatesAsync(studentId)).OrderBy(d => d).ToList();
        if (dates.Count == 0)
            return 0;

        var best = 1;
        var current = 1;
        for (var i = 1; i < dates.Count; i++)
        {
            if (dates[i] == dates[i - 1].AddDays(1))
                current++;
            else
                current = 1;
            best = Math.Max(best, current);
        }
        return best;
    }

    private async Task<HashSet<DateTime>> StudyDatesAsync(Guid studentId)
    {
        var lessonDates = await _db.Set<LessonProgress>()
            .Where(p => p.StudentId == studentId && p.LastAccessedAt != null)
            .Select(p => p.LastAccessedAt!.Value.Date)
            .ToListAsync();

        var sessionDates = await _db.Set<LearningSession>()
            .Where(s => s.StudentId == studentId)
            .Select(s => s.StartedAt.Date)
            .ToListAsync();

        return lessonDates.Concat(sessionDates).ToHashSet();
    }

    private static bool TryParseDay(string? value, out DayOfWeek day)
    {
        if (Enum.TryParse(value, true, out day))
            return true;
        if (int.TryParse(value, out var number) && number >= 0 && number <= 6)
        {
            day = (DayOfWeek)number;
            return true;
        }
        day = DayOfWeek.Monday;
        return false;
    }

    private static bool TryParseRange(string value, out TimeSpan start, out TimeSpan end)
    {
        start = default;
        end = default;
        var parts = value.Split('-', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 &&
               TimeSpan.TryParse(parts[0], CultureInfo.InvariantCulture, out start) &&
               TimeSpan.TryParse(parts[1], CultureInfo.InvariantCulture, out end) &&
               end > start;
    }

    private sealed class CourseProgressSummaryInternal
    {
        public int TotalLessons { get; init; }
        public int CompletedLessons { get; init; }
        public int TotalModules { get; init; }
        public int TotalModulesCompleted { get; init; }
        public double OverallProgress { get; init; }
        public DateTime? LastAccessedAt { get; init; }
    }

    private sealed record AchievementInternal(
        string id,
        string title,
        string description,
        string icon,
        bool isUnlocked,
        int progress,
        int target)
    {
        public object ToResponse() => new
        {
            id,
            title,
            description,
            icon,
            isUnlocked,
            unlockedAt = isUnlocked ? DateTime.UtcNow : (DateTime?)null,
            progress,
            target
        };
    }
}

public sealed class UpdateLessonProgressCompatibilityRequest
{
    public int? TimeSpent { get; set; }
    public int? LastPosition { get; set; }
}

public sealed class CreateAvailabilityCompatibilityRequest
{
    public string Day { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}
