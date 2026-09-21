using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Infrastructure.Persistence;
using SeOne.Domain.Entities;

namespace SeOne.Infrastructure.Services;

public class EnrollmentService : IEnrollmentService
{
    private readonly SeOneDbContext _context;

    public EnrollmentService(SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<EnrollmentDto?> EnrollAsync(Guid studentId, Guid courseId)
    {
        var course = await _context.Set<Course>().FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null)
            return null;

        if (!course.IsPublished)
            return null;

        var already = await _context.Set<Enrollment>().AnyAsync(e => e.CourseId == courseId && e.StudentId == studentId);

        if (already)
            return null;

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            EnrolledAt = DateTime.UtcNow
        };

        _context.Set<Enrollment>().Add(enrollment);
        await _context.SaveChangesAsync();

        return new EnrollmentDto
        {
            Id = enrollment.Id,
            CourseId = courseId,
            CourseTitle = course.Title,
            StudentId = studentId,
            EnrolledAt = enrollment.EnrolledAt
        };
    }

    public async Task<List<EnrollmentDto>> GetMyEnrollmentsAsync(Guid studentId)
    {
        return await _context.Set<Enrollment>()
            .Where(e => e.StudentId == studentId)
            .Include(e => e.Course)
            .OrderByDescending(e => e.EnrolledAt)
            .Select(e => new EnrollmentDto
            {
                Id = e.Id,
                CourseId = e.CourseId,
                CourseTitle = e.Course.Title,
                StudentId = e.StudentId,
                EnrolledAt = e.EnrolledAt
            })
            .ToListAsync();
    }

    public async Task<bool> IsEnrolledAsync(Guid studentId, Guid courseId)
    {
        return await _context.Set<Enrollment>().AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId);
    }

    public async Task<List<CourseEnrollmentStudentDto>> GetCourseStudentsAsync(Guid courseId, Guid teacherId)
    {
        var course = await _context.Set<Course>().FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null)
            return new List<CourseEnrollmentStudentDto>();

        if (course.TeacherId != teacherId)
            return new List<CourseEnrollmentStudentDto>();

        return await _context.Set<Enrollment>()
            .Where(e => e.CourseId == courseId)
            .Include(e => e.Student)
            .OrderBy(e => e.EnrolledAt)
            .Select(e => new CourseEnrollmentStudentDto
            {
                StudentId = e.StudentId,
                StudentName = e.Student.FullName,
                StudentEmail = e.Student.Email,
                EnrolledAt = e.EnrolledAt
            })
            .ToListAsync();
    }
}
