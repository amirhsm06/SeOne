using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class CourseLearningService : ICourseLearningService
{
    private readonly SeOneDbContext _context;

    public CourseLearningService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<CourseLearningDto?> GetCourseLearningAsync(Guid studentId, Guid courseId)
    {
        // load course and teacher
        var course = await _context.Set<Domain.Entities.Course>()
           .FirstOrDefaultAsync(c => c.Id == courseId);

        var teacher = course is null
            ? null
            : await _context.Set<CourseInstanceTeacher>()
                .Where(t => t.CourseInstance.CourseId == course.Id)
                .OrderBy(t => t.CourseInstance.CreatedAt)
                .ThenBy(t => t.CreatedAt)
                .Select(t => t.Teacher)
                .FirstOrDefaultAsync();

        if (course is null)
            return null;

        if (!course.IsPublished)
            return null;

        // check enrollment
        var enrolled = await _context.Set<Domain.Entities.Enrollment>()
            .AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId);

        if (!enrolled)
            return null;

        // load modules
        var modules = await _context.Set<Domain.Entities.CourseModule>()
            .Where(m => m.CourseId == courseId)
            .OrderBy(m => m.Order)
            .ToListAsync();

        var moduleIds = modules.Select(m => m.Id).ToList();

        // load lessons for modules
        var lessons = await _context.Set<Domain.Entities.Lesson>()
            .Where(l => moduleIds.Contains(l.CourseModuleId))
            .OrderBy(l => l.Order)
            .ToListAsync();

        var lessonIds = lessons.Select(l => l.Id).ToList();

        // load progress for this student only
        var progresses = await _context.Set<Domain.Entities.LessonProgress>()
            .Where(p => p.StudentId == studentId && lessonIds.Contains(p.LessonId) && p.IsCompleted)
            .ToListAsync();

        var courseDto = new CourseLearningDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            TeacherId = teacher?.Id ?? Guid.Empty,
            TeacherName = teacher?.FullName ?? string.Empty
        };

        // build modules
        int totalLessons = 0;
        int totalCompleted = 0;

        foreach (var module in modules)
        {
            var moduleLessons = lessons.Where(l => l.CourseModuleId == module.Id).OrderBy(l => l.Order).ToList();

            var moduleDto = new ModuleLearningDto
            {
                Id = module.Id,
                Title = module.Title,
                Description = module.Description,
                Order = module.Order
            };

            foreach (var lesson in moduleLessons)
            {
                var prog = progresses.FirstOrDefault(p => p.LessonId == lesson.Id);
                bool isCompleted = prog != null && prog.IsCompleted;

                moduleDto.Lessons.Add(new LessonLearningDto
                {
                    Id = lesson.Id,
                    Title = lesson.Title,
                    Content = lesson.Content,
                    Order = lesson.Order,
                    IsCompleted = isCompleted,
                    CompletedAt = prog?.CompletedAt
                });
            }

            moduleDto.TotalLessons = moduleDto.Lessons.Count;
            moduleDto.CompletedLessons = moduleDto.Lessons.Count(l => l.IsCompleted);
            moduleDto.Percentage = moduleDto.TotalLessons == 0 ? 0 : Math.Round((decimal)moduleDto.CompletedLessons / moduleDto.TotalLessons * 100, 2);

            totalLessons += moduleDto.TotalLessons;
            totalCompleted += moduleDto.CompletedLessons;

            courseDto.Modules.Add(moduleDto);
        }

        courseDto.TotalLessons = totalLessons;
        courseDto.CompletedLessons = totalCompleted;
        courseDto.Percentage = totalLessons == 0 ? 0 : Math.Round((decimal)totalCompleted / totalLessons * 100, 2);

        return courseDto;
    }
}
