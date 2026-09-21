using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class StudentDashboardService : IStudentDashboardService
{
    private readonly SeOneDbContext _context;

    public StudentDashboardService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<StudentDashboardDto?> GetMyDashboardAsync(Guid studentId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == studentId && u.Role == Domain.Enums.UserRole.Student);
        if (user is null)
            return null;

        // Get enrolled published courses for this student
        var enrollmentsQuery = _context.Set<Domain.Entities.Enrollment>()
            .Where(e => e.StudentId == studentId)
            .Join(_context.Set<Domain.Entities.Course>(),
                e => e.CourseId,
                c => c.Id,
                (e, c) => new { Enrollment = e, Course = c })
            .Where(x => x.Course.IsPublished);

        // Materialize course list projection with counts
        var courseProjections = await enrollmentsQuery
            .OrderByDescending(x => x.Enrollment.EnrolledAt)
            .Select(x => new StudentDashboardCourseDto
            {
                Id = x.Course.Id,
                Title = x.Course.Title,
                Level = x.Course.Level,
                Description = x.Course.Description,
                TeacherId = x.Course.TeacherId,
                TeacherName = x.Course.Teacher.FullName,
                Price = x.Course.Price,
                Duration = x.Course.Duration,
                ImageUrl = x.Course.ImageUrl,
                EnrolledAt = x.Enrollment.EnrolledAt,
                TotalLessons = _context.Set<Domain.Entities.Lesson>().Count(l => l.CourseModule.CourseId == x.Course.Id),
                CompletedLessons = _context.Set<Domain.Entities.LessonProgress>().Count(p => p.StudentId == studentId && _context.Set<Domain.Entities.Lesson>().Any(l => l.Id == p.LessonId && l.CourseModule.CourseId == x.Course.Id))
            })
            .ToListAsync();

        // Calculate Per-course progress percentage
        foreach (var c in courseProjections)
        {
            if (c.TotalLessons == 0)
                c.ProgressPercentage = 0m;
            else
                c.ProgressPercentage = Math.Round((decimal)c.CompletedLessons / c.TotalLessons * 100, 2);
        }

        var enrolledCourseCount = courseProjections.Count;

        var completedCourseCount = courseProjections.Count(c => c.TotalLessons > 0 && c.ProgressPercentage >= 100m);

        // Overall progress across all enrolled published courses
        var totalLessons = courseProjections.Sum(c => c.TotalLessons);
        var totalCompleted = courseProjections.Sum(c => c.CompletedLessons);

        decimal overall = 0m;
        if (totalLessons > 0)
            overall = Math.Round((decimal)totalCompleted / totalLessons * 100, 2);

        return new StudentDashboardDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            EnrolledCourseCount = enrolledCourseCount,
            CompletedCourseCount = completedCourseCount,
            OverallProgressPercentage = overall,
            Courses = courseProjections
        };
    }
}
