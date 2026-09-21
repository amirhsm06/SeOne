using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class ProgressService : IProgressService
{
    private readonly SeOneDbContext _context;

    public ProgressService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<LessonProgressDto?> UpdateLessonProgressAsync(Guid studentId, Guid lessonId, bool isCompleted)
    {
        // Find lesson and its module/course
        var lesson = await _context.Set<Lesson>()
            .Include(l => l.CourseModule)
            .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.Id == lessonId);

        if (lesson is null)
            return null;

        var course = lesson.CourseModule.Course;

        // Course must be published
        if (!course.IsPublished)
            return null;

        // Student must be enrolled in course
        var enrolled = await _context.Set<Enrollment>().AnyAsync(e => e.StudentId == studentId && e.CourseId == course.Id);

        if (!enrolled)
            return null;

        var progress = await _context.Set<LessonProgress>().FirstOrDefaultAsync(p => p.StudentId == studentId && p.LessonId == lessonId);

        if (progress is null)
        {
            progress = new LessonProgress
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                LessonId = lessonId,
                IsCompleted = isCompleted,
                CompletedAt = isCompleted ? DateTime.UtcNow : null
            };

            _context.Set<LessonProgress>().Add(progress);
        }
        else
        {
            progress.IsCompleted = isCompleted;
            progress.CompletedAt = isCompleted ? DateTime.UtcNow : null;
        }

        await _context.SaveChangesAsync();

        return new LessonProgressDto
        {
            LessonId = lessonId,
            IsCompleted = progress.IsCompleted,
            CompletedAt = progress.CompletedAt
        };
    }

    public async Task<LessonProgressDto?> GetLessonProgressAsync(Guid studentId, Guid lessonId)
    {
        var lesson = await _context.Set<Lesson>()
            .Include(l => l.CourseModule)
            .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.Id == lessonId);

        if (lesson is null)
            return null;

        var course = lesson.CourseModule.Course;

        if (!course.IsPublished)
            return null;

        var enrolled = await _context.Set<Enrollment>().AnyAsync(e => e.StudentId == studentId && e.CourseId == course.Id);

        if (!enrolled)
            return null;

        var progress = await _context.Set<LessonProgress>().FirstOrDefaultAsync(p => p.StudentId == studentId && p.LessonId == lessonId);

        if (progress is null)
        {
            return new LessonProgressDto
            {
                LessonId = lessonId,
                IsCompleted = false,
                CompletedAt = null
            };
        }

        return new LessonProgressDto
        {
            LessonId = lessonId,
            IsCompleted = progress.IsCompleted,
            CompletedAt = progress.CompletedAt
        };
    }

    public async Task<CourseProgressDto> GetCourseProgressAsync(Guid studentId, Guid courseId)
    {
        // Verify course exists and is published and student enrolled
        var course = await _context.Set<Course>().FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null || !course.IsPublished)
            return new CourseProgressDto { CourseId = courseId, TotalLessons = 0, CompletedLessons = 0, Percentage = 0 };

        var enrolled = await _context.Set<Enrollment>().AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId);

        if (!enrolled)
            return new CourseProgressDto { CourseId = courseId, TotalLessons = 0, CompletedLessons = 0, Percentage = 0 };

        var totalLessons = await _context.Set<Lesson>()
            .Where(l => l.CourseModule.CourseId == courseId)
            .CountAsync();

        var completedLessons = await _context.Set<LessonProgress>()
            .Where(lp => lp.StudentId == studentId && lp.IsCompleted)
            .Join(_context.Set<Lesson>(), lp => lp.LessonId, l => l.Id, (lp, l) => new { l })
            .CountAsync(lj => lj.l.CourseModule.CourseId == courseId);

        double percentage = totalLessons == 0 ? 0 : (completedLessons / (double)totalLessons) * 100.0;

        return new CourseProgressDto
        {
            CourseId = courseId,
            TotalLessons = totalLessons,
            CompletedLessons = completedLessons,
            Percentage = percentage
        };
    }

    public async Task<ModuleProgressDto> GetModuleProgressAsync(Guid studentId, Guid moduleId)
    {
        var module = await _context.Set<CourseModule>()
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == moduleId);

        if (module is null || !module.Course.IsPublished)
            return new ModuleProgressDto { ModuleId = moduleId, TotalLessons = 0, CompletedLessons = 0, Percentage = 0 };

        var enrolled = await _context.Set<Enrollment>().AnyAsync(e => e.StudentId == studentId && e.CourseId == module.CourseId);

        if (!enrolled)
            return new ModuleProgressDto { ModuleId = moduleId, TotalLessons = 0, CompletedLessons = 0, Percentage = 0 };

        var totalLessons = await _context.Set<Lesson>().Where(l => l.CourseModuleId == moduleId).CountAsync();

        var completedLessons = await _context.Set<LessonProgress>()
            .Where(lp => lp.StudentId == studentId && lp.IsCompleted)
            .Join(_context.Set<Lesson>(), lp => lp.LessonId, l => l.Id, (lp, l) => new { l })
            .CountAsync(x => x.l.CourseModuleId == moduleId);

        double percentage = totalLessons == 0 ? 0 : (completedLessons / (double)totalLessons) * 100.0;

        return new ModuleProgressDto
        {
            ModuleId = moduleId,
            TotalLessons = totalLessons,
            CompletedLessons = completedLessons,
            Percentage = percentage
        };
    }
}
