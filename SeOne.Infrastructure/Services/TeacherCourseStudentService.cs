using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class TeacherCourseStudentService : ITeacherCourseStudentService
{
    private readonly SeOneDbContext _context;

    public TeacherCourseStudentService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<List<TeacherCourseStudentDto>> GetStudentsForCourseAsync(Guid teacherId, Guid courseId)
    {
        // Verify course exists and belongs to teacher
        var course = await _context.Set<Domain.Entities.Course>()
            .FirstOrDefaultAsync(c => c.Id == courseId && c.TeacherId == teacherId);

        if (course is null)
            return new List<TeacherCourseStudentDto>();

        // total lessons for course
        var totalLessons = await _context.Set<Domain.Entities.Lesson>()
            .Where(l => l.CourseModule.CourseId == courseId)
            .CountAsync();

        // Query enrollments and project student info and completed lessons count
        var query = _context.Set<Domain.Entities.Enrollment>()
            .Where(e => e.CourseId == courseId)
            .OrderBy(e => e.EnrolledAt)
            .Select(e => new
            {
                e.StudentId,
                e.EnrolledAt,
                StudentFullName = e.Student.FullName,
                StudentEmail = e.Student.Email,
                CompletedLessons = _context.Set<Domain.Entities.LessonProgress>()
                    .Where(p => p.StudentId == e.StudentId && p.IsCompleted)
                    .Join(_context.Set<Domain.Entities.Lesson>(), p => p.LessonId, l => l.Id, (p, l) => l)
                    .Count(l => l.CourseModule.CourseId == courseId)
            });

        var list = await query.ToListAsync();

        var result = list.Select(item => new TeacherCourseStudentDto
        {
            StudentId = item.StudentId,
            FullName = item.StudentFullName,
            Email = item.StudentEmail,
            EnrolledAt = item.EnrolledAt,
            TotalLessons = totalLessons,
            CompletedLessons = item.CompletedLessons,
            ProgressPercentage = totalLessons == 0 ? 0m : Math.Round((decimal)item.CompletedLessons / totalLessons * 100, 2)
        }).ToList();

        return result;
    }
}
