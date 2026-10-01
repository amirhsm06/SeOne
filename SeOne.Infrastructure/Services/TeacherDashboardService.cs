using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class TeacherDashboardService : ITeacherDashboardService
{
    private readonly SeOneDbContext _context;

    public TeacherDashboardService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<TeacherDashboardDto?> GetMyDashboardAsync(Guid teacherId)
    {
        // Load user and profile
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.Role == UserRole.Teacher);
        if (user is null)
            return null;

        var profile = await _context.Set<TeacherProfile>().FirstOrDefaultAsync(p => p.TeacherId == teacherId);

        // Courses projection with counts
        var coursesQuery = _context.Set<Course>()
          .Where(c =>
              _context.Set<CourseInstanceTeacher>()
                  .Any(t =>
                      t.TeacherId == teacherId &&
                      t.CourseInstance.CourseId == c.Id));

        var courses = await coursesQuery
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new TeacherCourseSummaryDto
            {
                Id = c.Id,
                Title = c.Title,
                Level = c.Level,
                IsPublished = c.IsPublished,
                StudentCount = _context.Set<Domain.Entities.Enrollment>().Count(e => e.CourseId == c.Id),
                LessonCount = _context.Set<Domain.Entities.Lesson>().Count(l => l.CourseModule.CourseId == c.Id),
                Price = c.Price,
                Duration = c.Duration,
                ImageUrl = c.ImageUrl
            })
            .ToListAsync();

        // Summary stats
        var totalCourses = await coursesQuery.CountAsync();
        var publishedCourses = await coursesQuery.CountAsync(c => c.IsPublished);

        // total distinct students across teacher courses
        var studentCount = await _context.Set<Domain.Entities.Enrollment>()
            .Where(e =>
                _context.Set<CourseInstanceTeacher>()
                    .Any(t =>
                        t.TeacherId == teacherId &&
                        t.CourseInstance.CourseId == e.CourseId))
            .Select(e => e.StudentId)
            .Distinct()
            .CountAsync();

        // Availability
        var availability = await _context.Set<Domain.Entities.TeacherAvailability>()
            .Where(a => a.TeacherId == teacherId)
            .OrderBy(a => a.DayOfWeek)
            .ThenBy(a => a.StartTime)
            .Select(a => new TeacherAvailabilityItemDto
            {
                Id = a.Id,
                DayOfWeek = a.DayOfWeek,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                IsAvailable = a.IsAvailable
            })
            .ToListAsync();

        return new TeacherDashboardDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Avatar = profile?.Avatar,
            TeachingLanguage = profile?.TeachingLanguage,
            Subject = profile?.Subject,
            Level = profile?.Level,
            Rating = profile?.Rating ?? 0m,
            TotalCourses = totalCourses,
            PublishedCourses = publishedCourses,
            TotalStudents = studentCount,
            Courses = courses,
            Availability = availability
        };
    }
}
