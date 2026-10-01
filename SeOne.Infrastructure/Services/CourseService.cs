using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class CourseService : ICourseService
{
    private readonly SeOneDbContext _context;

    public CourseService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<CourseDto?> UpdateAsync(
        Guid courseId,
        Guid teacherId,
        string title,
        string description,
        string level,
        decimal price,
        string? duration,
        string? imageUrl)
    {
        var course = await _context.Set<Course>()
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null)
            return null;

        if (!await TeacherHasCourseAccessAsync(courseId, teacherId))
            return null;

        course.Title = title;
        course.Description = description;
        course.Level = level;
        course.Price = price;
        course.Duration = duration;
        course.ImageUrl = imageUrl;

        await _context.SaveChangesAsync();

        return await GetCourseDtoAsync(courseId);
    }

    public async Task<bool> DeleteAsync(Guid courseId, Guid teacherId)
    {
        var courseExists = await _context.Set<Course>()
            .AnyAsync(c => c.Id == courseId);

        if (!courseExists)
            return false;

        if (!await TeacherHasCourseAccessAsync(courseId, teacherId))
            return false;

        using var tx = await _context.Database.BeginTransactionAsync();

        try
        {
            var moduleIds = await _context.Set<CourseModule>()
                .Where(m => m.CourseId == courseId)
                .Select(m => m.Id)
                .ToListAsync();

            var lessonIds = new List<Guid>();

            if (moduleIds.Count > 0)
            {
                lessonIds = await _context.Set<Lesson>()
                    .Where(l => moduleIds.Contains(l.CourseModuleId))
                    .Select(l => l.Id)
                    .ToListAsync();
            }

            if (lessonIds.Count > 0)
            {
                await _context.Set<LearningSession>()
                    .Where(s => lessonIds.Contains(s.LessonId))
                    .ExecuteDeleteAsync();

                await _context.Set<LessonProgress>()
                    .Where(p => lessonIds.Contains(p.LessonId))
                    .ExecuteDeleteAsync();

                await _context.Set<Lesson>()
                    .Where(l => lessonIds.Contains(l.Id))
                    .ExecuteDeleteAsync();
            }

            if (moduleIds.Count > 0)
            {
                await _context.Set<CourseModule>()
                    .Where(m => moduleIds.Contains(m.Id))
                    .ExecuteDeleteAsync();
            }

            var practiceIds = await _context.Set<Practice>()
                .Where(x => x.CourseId == courseId)
                .Select(x => x.Id)
                .ToListAsync();

            if (practiceIds.Count > 0)
            {
                await _context.Set<PracticeAttemptAnswer>()
                    .Where(x => practiceIds.Contains(x.Attempt.PracticeId))
                    .ExecuteDeleteAsync();

                await _context.Set<PracticeAttempt>()
                    .Where(x => practiceIds.Contains(x.PracticeId))
                    .ExecuteDeleteAsync();

                await _context.Set<PracticeQuestion>()
                    .Where(x => practiceIds.Contains(x.PracticeId))
                    .ExecuteDeleteAsync();

                await _context.Set<Practice>()
                    .Where(x => practiceIds.Contains(x.Id))
                    .ExecuteDeleteAsync();
            }

            await _context.Set<Enrollment>()
                .Where(e => e.CourseId == courseId)
                .ExecuteDeleteAsync();

            await _context.Set<Conversation>()
                .Where(x => x.RelatedCourseId == courseId)
                .ExecuteDeleteAsync();

            var instanceIds = await _context.Set<CourseInstance>()
                .Where(x => x.CourseId == courseId)
                .Select(x => x.Id)
                .ToListAsync();

            if (instanceIds.Count > 0)
            {
                await _context.Set<CourseInstanceTeacher>()
                    .Where(x => instanceIds.Contains(x.CourseInstanceId))
                    .ExecuteDeleteAsync();

                await _context.Set<CourseInstance>()
                    .Where(x => instanceIds.Contains(x.Id))
                    .ExecuteDeleteAsync();
            }

            await _context.Set<Course>()
                .Where(c => c.Id == courseId)
                .ExecuteDeleteAsync();

            await tx.CommitAsync();

            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<CourseDto>> GetAllAsync(Guid userId, string userRole)
    {
        var query = _context.Set<Course>().AsQueryable();

        if (string.Equals(userRole, "Student", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsPublished);
        }
        else if (string.Equals(userRole, "Teacher", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x =>
                _context.Set<CourseInstanceTeacher>()
                    .Any(t =>
                        t.TeacherId == userId &&
                        t.CourseInstance.CourseId == x.Id));
        }
        else
        {
            query = query.Where(x => x.IsPublished);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CourseDto
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                Level = x.Level,
                Price = x.Price,
                Currency = x.Currency,
                DiscountPercent = x.DiscountPercent,
                Duration = x.Duration,
                ImageUrl = x.ImageUrl,
                Category = x.Category,
                Language = x.Language,
                IsFeatured = x.IsFeatured,
                IsPublished = x.IsPublished,
                CreatedAt = x.CreatedAt,

                TeacherId = _context.Set<CourseInstanceTeacher>()
                    .Where(t => t.CourseInstance.CourseId == x.Id)
                    .OrderBy(t => t.CourseInstance.CreatedAt)
                    .ThenBy(t => t.CreatedAt)
                    .Select(t => t.TeacherId)
                    .FirstOrDefault(),

                TeacherName = _context.Set<CourseInstanceTeacher>()
                    .Where(t => t.CourseInstance.CourseId == x.Id)
                    .OrderBy(t => t.CourseInstance.CreatedAt)
                    .ThenBy(t => t.CreatedAt)
                    .Select(t => t.Teacher.FullName)
                    .FirstOrDefault() ?? string.Empty
            })
            .ToListAsync();
    }

    public async Task<CourseDto?> GetByIdAsync(Guid id, Guid userId, string userRole)
    {
        var query = _context.Set<Course>()
            .Where(x => x.Id == id);

        if (string.Equals(userRole, "Student", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsPublished);
        }
        else if (string.Equals(userRole, "Teacher", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x =>
                _context.Set<CourseInstanceTeacher>()
                    .Any(t =>
                        t.TeacherId == userId &&
                        t.CourseInstance.CourseId == x.Id));
        }
        else
        {
            query = query.Where(x => x.IsPublished);
        }

        return await query
            .Select(x => new CourseDto
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                Level = x.Level,
                Price = x.Price,
                Currency = x.Currency,
                DiscountPercent = x.DiscountPercent,
                Duration = x.Duration,
                ImageUrl = x.ImageUrl,
                Category = x.Category,
                Language = x.Language,
                IsFeatured = x.IsFeatured,
                IsPublished = x.IsPublished,
                CreatedAt = x.CreatedAt,

                TeacherId = _context.Set<CourseInstanceTeacher>()
                    .Where(t => t.CourseInstance.CourseId == x.Id)
                    .OrderBy(t => t.CourseInstance.CreatedAt)
                    .ThenBy(t => t.CreatedAt)
                    .Select(t => t.TeacherId)
                    .FirstOrDefault(),

                TeacherName = _context.Set<CourseInstanceTeacher>()
                    .Where(t => t.CourseInstance.CourseId == x.Id)
                    .OrderBy(t => t.CourseInstance.CreatedAt)
                    .ThenBy(t => t.CreatedAt)
                    .Select(t => t.Teacher.FullName)
                    .FirstOrDefault() ?? string.Empty
            })
            .FirstOrDefaultAsync();
    }

    public async Task<bool> SetPublishedAsync(
        Guid courseId,
        Guid teacherId,
        bool isPublished)
    {
        var course = await _context.Set<Course>()
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null)
            return false;

        if (!await TeacherHasCourseAccessAsync(courseId, teacherId))
            return false;

        course.IsPublished = isPublished;

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<CourseDto> CreateAsync(
        string title,
        string description,
        string level,
        decimal price,
        string? duration,
        string? imageUrl,
        Guid teacherId)
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Level = level,
            Price = price,
            Duration = duration,
            ImageUrl = imageUrl,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow
        };

        var instance = new CourseInstance
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StartDate = null,
            EndDate = null,
            CreatedAt = DateTime.UtcNow
        };

        var instanceTeacher = new CourseInstanceTeacher
        {
            Id = Guid.NewGuid(),
            CourseInstanceId = instance.Id,
            TeacherId = teacherId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<Course>().Add(course);
        _context.Set<CourseInstance>().Add(instance);
        _context.Set<CourseInstanceTeacher>().Add(instanceTeacher);

        await _context.SaveChangesAsync();

        var teacherName = await _context.Users
            .Where(x => x.Id == teacherId)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync() ?? string.Empty;

        return new CourseDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            Level = course.Level,
            Price = course.Price,
            Currency = course.Currency,
            DiscountPercent = course.DiscountPercent,
            Duration = course.Duration,
            ImageUrl = course.ImageUrl,
            Category = course.Category,
            Language = course.Language,
            IsFeatured = course.IsFeatured,
            IsPublished = course.IsPublished,
            CreatedAt = course.CreatedAt,
            TeacherId = teacherId,
            TeacherName = teacherName
        };
    }

    public async Task<List<CourseCatalogDto>> GetPublishedCatalogAsync(string lang)
    {
        var query = _context.Set<Course>()
            .Where(c => c.IsPublished);

        return await query
            .Select(c => new CourseCatalogDto
            {
                Id = c.Id,
                Title = c.Title,
                Level = c.Level,
                Description = c.Description,
                LessonCount = _context.Set<Lesson>()
                    .Count(l => l.CourseModule.CourseId == c.Id),
                StudentCount = _context.Set<Enrollment>()
                    .Count(e => e.CourseId == c.Id),
                Price = c.DiscountPercent > 0
                    ? c.Price * (1m - c.DiscountPercent / 100m)
                    : c.Price,
                BasePrice = c.Price,
                Currency = c.Currency,
                DiscountPercent = c.DiscountPercent,
                Duration = c.Duration,
                ImageUrl = c.ImageUrl,
                Category = c.Category,
                Language = c.Language,
                IsFeatured = c.IsFeatured
            })
            .ToListAsync();
    }

    private async Task<bool> TeacherHasCourseAccessAsync(
        Guid courseId,
        Guid teacherId)
    {
        return await _context.Set<CourseInstanceTeacher>()
            .AnyAsync(x =>
                x.TeacherId == teacherId &&
                x.CourseInstance.CourseId == courseId);
    }

    private async Task<CourseDto?> GetCourseDtoAsync(Guid courseId)
    {
        return await _context.Set<Course>()
            .Where(x => x.Id == courseId)
            .Select(x => new CourseDto
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                Level = x.Level,
                Price = x.Price,
                Currency = x.Currency,
                DiscountPercent = x.DiscountPercent,
                Duration = x.Duration,
                ImageUrl = x.ImageUrl,
                Category = x.Category,
                Language = x.Language,
                IsFeatured = x.IsFeatured,
                IsPublished = x.IsPublished,
                CreatedAt = x.CreatedAt,

                TeacherId = _context.Set<CourseInstanceTeacher>()
                    .Where(t => t.CourseInstance.CourseId == x.Id)
                    .OrderBy(t => t.CourseInstance.CreatedAt)
                    .ThenBy(t => t.CreatedAt)
                    .Select(t => t.TeacherId)
                    .FirstOrDefault(),

                TeacherName = _context.Set<CourseInstanceTeacher>()
                    .Where(t => t.CourseInstance.CourseId == x.Id)
                    .OrderBy(t => t.CourseInstance.CreatedAt)
                    .ThenBy(t => t.CreatedAt)
                    .Select(t => t.Teacher.FullName)
                    .FirstOrDefault() ?? string.Empty
            })
            .FirstOrDefaultAsync();
    }
}