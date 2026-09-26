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

    public async Task<CourseDto?> UpdateAsync(Guid courseId, Guid teacherId, string title, string description, string level, decimal price, string? duration, string? imageUrl)
    {
        var course = await _context.Set<Domain.Entities.Course>()
            .Include(c => c.Teacher)
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null)
            return null;

        if (course.TeacherId != teacherId)
            return null;

        // Only update allowed fields
        course.Title = title;
        course.Description = description;
        course.Level = level;
        course.Price = price;
        course.Duration = duration;
        course.ImageUrl = imageUrl;

        await _context.SaveChangesAsync();

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
            TeacherId = course.TeacherId,
            TeacherName = course.Teacher?.FullName ?? string.Empty
        };
    }

    public async Task<bool> DeleteAsync(Guid courseId, Guid teacherId)
    {
        var course = await _context.Set<Domain.Entities.Course>()
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null)
            return false;

        if (course.TeacherId != teacherId)
            return false;

        // Perform dependent deletes in a transaction to avoid leaving orphaned records.
        // Deletion order: LessonProgress -> Lessons -> CourseModules -> Practice -> Enrollments -> Course
        // Use ExecuteDeleteAsync where possible to perform server-side deletes without loading entities.
        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            // Get module ids for the course
            var moduleIds = await _context.Set<Domain.Entities.CourseModule>()
                .Where(m => m.CourseId == courseId)
                .Select(m => m.Id)
                .ToListAsync();

            // Get lesson ids for those modules
            var lessonIds = new List<Guid>();
            if (moduleIds.Count > 0)
            {
                lessonIds = await _context.Set<Domain.Entities.Lesson>()
                    .Where(l => moduleIds.Contains(l.CourseModuleId))
                    .Select(l => l.Id)
                    .ToListAsync();
            }

            if (lessonIds.Count > 0)
            {
                // delete learning sessions first because LearningSession -> Lesson is restrictive.
                await _context.Set<Domain.Entities.LearningSession>()
                    .Where(s => lessonIds.Contains(s.LessonId))
                    .ExecuteDeleteAsync();

                // delete lesson progress for these lessons
                await _context.Set<Domain.Entities.LessonProgress>()
                    .Where(p => lessonIds.Contains(p.LessonId))
                    .ExecuteDeleteAsync();

                // delete lessons
                await _context.Set<Domain.Entities.Lesson>()
                    .Where(l => lessonIds.Contains(l.Id))
                    .ExecuteDeleteAsync();
            }

            if (moduleIds.Count > 0)
            {
                // delete modules
                await _context.Set<Domain.Entities.CourseModule>()
                    .Where(m => moduleIds.Contains(m.Id))
                    .ExecuteDeleteAsync();
            }

            // delete practice data for the course before deleting the course.
            // Practice uses restrictive foreign keys to avoid SQL Server cascade-path issues.
            var practiceIds = await _context.Set<Domain.Entities.Practice>()
                .Where(x => x.CourseId == courseId)
                .Select(x => x.Id)
                .ToListAsync();

            if (practiceIds.Count > 0)
            {
                await _context.Set<Domain.Entities.PracticeAttemptAnswer>()
                    .Where(x => practiceIds.Contains(x.Attempt.PracticeId))
                    .ExecuteDeleteAsync();

                await _context.Set<Domain.Entities.PracticeAttempt>()
                    .Where(x => practiceIds.Contains(x.PracticeId))
                    .ExecuteDeleteAsync();

                await _context.Set<Domain.Entities.PracticeQuestion>()
                    .Where(x => practiceIds.Contains(x.PracticeId))
                    .ExecuteDeleteAsync();

                await _context.Set<Domain.Entities.Practice>()
                    .Where(x => practiceIds.Contains(x.Id))
                    .ExecuteDeleteAsync();
            }

            // delete enrollments for the course
            await _context.Set<Domain.Entities.Enrollment>()
                .Where(e => e.CourseId == courseId)
                .ExecuteDeleteAsync();

            await _context.Set<Conversation>()
                .Where(x => x.RelatedCourseId == courseId)
                .ExecuteDeleteAsync();

            // finally delete the course
            await _context.Set<Domain.Entities.Course>()
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
        var query = _context.Set<Domain.Entities.Course>().Include(x => x.Teacher).AsQueryable();

        if (string.Equals(userRole, "Student", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsPublished);
        }
        else if (string.Equals(userRole, "Teacher", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.TeacherId == userId);
        }
        else
        {
            // default to published for unknown roles
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
                TeacherId = x.TeacherId,
                TeacherName = x.Teacher.FullName
            })
            .ToListAsync();
    }

    public async Task<CourseDto?> GetByIdAsync(Guid id, Guid userId, string userRole)
    {
        var course = await _context.Set<Domain.Entities.Course>()
            .Include(x => x.Teacher)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (course is null)
            return null;

        if (string.Equals(userRole, "Student", StringComparison.OrdinalIgnoreCase))
        {
            if (!course.IsPublished)
                return null;
        }
        else if (string.Equals(userRole, "Teacher", StringComparison.OrdinalIgnoreCase))
        {
            if (course.TeacherId != userId)
                return null;
        }
        else
        {
            // default: only published
            if (!course.IsPublished)
                return null;
        }

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
            TeacherId = course.TeacherId,
            TeacherName = course.Teacher.FullName
        };
    }

    public async Task<bool> SetPublishedAsync(Guid courseId, Guid teacherId, bool isPublished)
    {
        var course = await _context.Set<Domain.Entities.Course>()
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null)
            return false;

        if (course.TeacherId != teacherId)
            return false;

        course.IsPublished = isPublished;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<CourseDto> CreateAsync(string title, string description, string level, decimal price, string? duration, string? imageUrl, Guid teacherId)
    {
        var course = new Domain.Entities.Course
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Level = level,
            Price = price,
            Duration = duration,
            ImageUrl = imageUrl,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow,
            TeacherId = teacherId
        };

        _context.Set<Domain.Entities.Course>().Add(course);
        await _context.SaveChangesAsync();

        // Load teacher full name
        var teacher = await _context.Users.FindAsync(teacherId);
        var teacherName = teacher?.FullName ?? string.Empty;

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
            TeacherId = course.TeacherId,
            TeacherName = teacherName
        };
    }

    public async Task<List<CourseCatalogDto>> GetPublishedCatalogAsync(string lang)
    {
        // lang parameter currently unused — reserved for future localization
        var query = _context.Set<Domain.Entities.Course>().Where(c => c.IsPublished);

        return await query.Select(c => new CourseCatalogDto
        {
            Id = c.Id,
            Title = c.Title,
            Level = c.Level,
            Description = c.Description,
            LessonCount = _context.Set<Domain.Entities.Lesson>().Count(l => l.CourseModule.CourseId == c.Id),
            StudentCount = _context.Set<Domain.Entities.Enrollment>().Count(e => e.CourseId == c.Id),
            Price = c.DiscountPercent > 0 ? c.Price * (1m - c.DiscountPercent / 100m) : c.Price,
            BasePrice = c.Price,
            Currency = c.Currency,
            DiscountPercent = c.DiscountPercent,
            Duration = c.Duration,
            ImageUrl = c.ImageUrl,
            Category = c.Category,
            Language = c.Language,
            IsFeatured = c.IsFeatured
        }).ToListAsync();
    }
}