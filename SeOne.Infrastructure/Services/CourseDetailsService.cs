using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class CourseDetailsService : ICourseDetailsService
{
    private readonly SeOneDbContext _context;

    public CourseDetailsService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<CourseDetailsDto?> GetCourseDetailsAsync(Guid courseId, Guid? userId, string userRole)
    {
        // Load basic course and teacher
        var courseQuery = _context.Set<Domain.Entities.Course>().AsQueryable();

        var course = await courseQuery
            .Include(c => c.Teacher)
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null)
            return null; // not found

        // determine visibility
        bool isOwner = false;
        if (!string.IsNullOrEmpty(userRole) && string.Equals(userRole, "Teacher", StringComparison.OrdinalIgnoreCase) && userId.HasValue)
        {
            isOwner = course.TeacherId == userId.Value;
        }

        if (!course.IsPublished && !isOwner)
        {
            // Not published and not owner -> treat as not found (do not leak)
            return null;
        }

        // Build base dto
        var dto = new CourseDetailsDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            Level = course.Level,
            Price = course.Price,
            Duration = course.Duration,
            ImageUrl = course.ImageUrl,
            TeacherId = course.TeacherId,
            TeacherName = course.Teacher?.FullName ?? string.Empty,
            TotalModuleCount = await _context.Set<Domain.Entities.CourseModule>().CountAsync(m => m.CourseId == course.Id),
            TotalLessonCount = await _context.Set<Domain.Entities.Lesson>().CountAsync(l => l.CourseModule.CourseId == course.Id)
        };

        // teacher profile summary
        var profile = await _context.Set<Domain.Entities.TeacherProfile>().FirstOrDefaultAsync(p => p.TeacherId == course.TeacherId);
        if (profile is not null)
        {
            dto.TeacherProfile = new TeacherProfileSummaryDto
            {
                TeacherId = profile.TeacherId,
                FullName = course.Teacher?.FullName ?? string.Empty,
                Avatar = profile.Avatar,
                TeachingLanguage = profile.TeachingLanguage,
                Subject = profile.Subject,
                Level = profile.Level,
                Rating = profile.Rating
            };
        }

        // student-specific info
        if (!string.IsNullOrEmpty(userRole) && string.Equals(userRole, "Student", StringComparison.OrdinalIgnoreCase) && userId.HasValue)
        {
            var enrolled = await _context.Set<Domain.Entities.Enrollment>().AnyAsync(e => e.StudentId == userId.Value && e.CourseId == course.Id);
            dto.IsEnrolled = enrolled;

            // calculate progresses for this student
            var totalLessons = dto.TotalLessonCount;
            if (totalLessons == 0)
            {
                dto.ProgressPercentage = 0m;
            }
            else
            {
                var completed = await _context.Set<Domain.Entities.LessonProgress>()
                    .Where(p => p.StudentId == userId.Value)
                    .Join(_context.Set<Domain.Entities.Lesson>(), p => p.LessonId, l => l.Id, (p, l) => new { p, l })
                    .CountAsync(x => x.p.IsCompleted && x.l.CourseModule.CourseId == course.Id);

                dto.ProgressPercentage = Math.Round((decimal)completed / totalLessons * 100, 2);
            }
        }

        // If teacher owner, include modules and lessons
        if (isOwner)
        {
            dto.IsOwner = true;

            var modules = await _context.Set<Domain.Entities.CourseModule>()
                .Where(m => m.CourseId == course.Id)
                .OrderBy(m => m.Order)
                .ToListAsync();

            var moduleIds = modules.Select(m => m.Id).ToList();

            var lessons = await _context.Set<Domain.Entities.Lesson>()
                .Where(l => moduleIds.Contains(l.CourseModuleId))
                .OrderBy(l => l.Order)
                .ToListAsync();

            dto.Modules = modules.Select(m => new CourseDetailsModuleDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                Order = m.Order,
                Lessons = lessons.Where(l => l.CourseModuleId == m.Id).Select(l => new CourseDetailsLessonDto
                {
                    Id = l.Id,
                    Title = l.Title,
                    Order = l.Order
                }).ToList()
            }).ToList();
        }

        return dto;
    }
}
