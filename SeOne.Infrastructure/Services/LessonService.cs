using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Infrastructure.Persistence;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Services;

public class LessonService : ILessonService
{
    private readonly SeOneDbContext _context;
    private readonly IEnrollmentService _enrollmentService;

    public LessonService(SeOneDbContext context, IEnrollmentService enrollmentService)
    {
        _context = context;
        _enrollmentService = enrollmentService;
    }

    public async Task<LessonDto?> CreateAsync(Guid teacherId, Guid courseModuleId, string title, string content, int order)
    {
        var module = await _context.Set<CourseModule>()
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == courseModuleId);

        if (module is null)
            return null;

        if (!await _context.Set<CourseInstanceTeacher>()
            .AnyAsync(t =>
                t.TeacherId == teacherId &&
                t.CourseInstance.CourseId == module.CourseId))
        {
            return null;
        }

        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            CourseModuleId = courseModuleId,
            Title = title,
            Content = content,
            Order = order,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<Lesson>().Add(lesson);
        await _context.SaveChangesAsync();

        return new LessonDto
        {
            Id = lesson.Id,
            CourseModuleId = lesson.CourseModuleId,
            Title = lesson.Title,
            Content = lesson.Content,
            Order = lesson.Order,
            CreatedAt = lesson.CreatedAt
        };
    }

    public async Task<List<LessonDto>?> GetByModuleAsync(Guid courseModuleId, Guid userId, string userRole)
    {
        var module = await _context.Set<CourseModule>()
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == courseModuleId);

        if (module is null)
            return null;

        if (string.Equals(userRole, "Student", StringComparison.OrdinalIgnoreCase))
        {
            // Students can only access published courses and must be enrolled
            if (!module.Course.IsPublished)
                return null;

            var enrolled = await _enrollmentService.IsEnrolledAsync(userId, module.Course.Id);
            if (!enrolled)
                return null;
        }
        else if (string.Equals(userRole, "Teacher", StringComparison.OrdinalIgnoreCase))
        {
            var hasAccess = await _context.Set<CourseInstanceTeacher>()
                .AnyAsync(t =>
                    t.TeacherId == userId &&
                    t.CourseInstance.CourseId == module.CourseId);

            if (!hasAccess)
                return null;
        }
        else
        {
            if (!module.Course.IsPublished)
                return null;
        }

        return await _context.Set<Lesson>()
            .Where(l => l.CourseModuleId == courseModuleId)
            .OrderBy(l => l.Order)
            .Select(l => new LessonDto
            {
                Id = l.Id,
                CourseModuleId = l.CourseModuleId,
                Title = l.Title,
                Content = l.Content,
                Order = l.Order,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<LessonDto?> UpdateAsync(Guid courseModuleId, Guid lessonId, Guid teacherId, string title, string content, int order)
    {

        // Validate lesson exists, belongs to module, and teacher owns parent course
        var lesson = await _context.Set<Lesson>()
            .Include(l => l.CourseModule)
            .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.Id == lessonId);

        if (lesson is null)
            return null;

        if (lesson.CourseModuleId != courseModuleId)
            return null;

        if (!await _context.Set<CourseInstanceTeacher>()
            .AnyAsync(t =>
                t.TeacherId == teacherId &&
                t.CourseInstance.CourseId == lesson.CourseModule.CourseId))
        {
            return null;
        }

        lesson.Title = title;
        lesson.Content = content;
        lesson.Order = order;

        await _context.SaveChangesAsync();

        return new LessonDto
        {
            Id = lesson.Id,
            CourseModuleId = lesson.CourseModuleId,
            Title = lesson.Title,
            Content = lesson.Content,
            Order = lesson.Order,
            CreatedAt = lesson.CreatedAt
        };
    }

    public async Task<bool> DeleteAsync(Guid courseModuleId, Guid lessonId, Guid teacherId)
    {
        var lesson = await _context.Set<Lesson>()
            .Include(l => l.CourseModule)
            .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.Id == lessonId);

        if (lesson is null)
            return false;

        if (lesson.CourseModuleId != courseModuleId)
            return false;

        if (!await _context.Set<CourseInstanceTeacher>()
            .AnyAsync(t =>
                t.TeacherId == teacherId &&
                t.CourseInstance.CourseId == lesson.CourseModule.CourseId))
        {
            return false;
        }

        _context.Set<Lesson>().Remove(lesson);
        await _context.SaveChangesAsync();

        return true;
    }
}
