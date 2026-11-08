using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

/// <summary>Admin read endpoint used by the course editor to list enrolled students.</summary>
[ApiController]
[Route("api/admin/courses/{courseId:guid}/enrollments")]
[Authorize(Roles = "Admin")]
public sealed class AdminCourseEnrollmentsController : ControllerBase
{
    private readonly SeOneDbContext _db;

    public AdminCourseEnrollmentsController(SeOneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetCourseEnrollments(
        Guid courseId,
        CancellationToken cancellationToken)
    {
        var courseExists = await _db.Set<Course>()
            .AsNoTracking()
            .AnyAsync(course => course.Id == courseId, cancellationToken);

        if (!courseExists)
            return NotFound(new ProblemDetails
            {
                Title = "Course not found.",
                Status = StatusCodes.Status404NotFound
            });

        var enrollments = await _db.Set<Enrollment>()
            .AsNoTracking()
            .Where(enrollment => enrollment.CourseId == courseId)
            .Include(enrollment => enrollment.Student)
            .OrderByDescending(enrollment => enrollment.EnrolledAt)
            .ToListAsync(cancellationToken);

        var response = enrollments.Select(enrollment => new
        {
            id = enrollment.Id,
            courseId = enrollment.CourseId,
            studentId = enrollment.StudentId,
            studentName = enrollment.Student == null
                ? string.Empty
                : enrollment.Student.FullName,
            studentEmail = enrollment.Student == null
                ? string.Empty
                : enrollment.Student.Email ?? string.Empty,
            status = ReadEnrollmentStatus(enrollment),
            enrolledAt = DateTime.SpecifyKind(enrollment.EnrolledAt, DateTimeKind.Utc)
        }).ToList();

        return Ok(response);
    }

    // Older SeOne enrollment projections expose every persisted Enrollment as active.
    // Read a Status property when the current entity has one; otherwise retain that legacy contract.
    private static string ReadEnrollmentStatus(Enrollment enrollment)
    {
        var raw = typeof(Enrollment)
            .GetProperty("Status", BindingFlags.Instance | BindingFlags.Public)?
            .GetValue(enrollment)?
            .ToString();

        return string.IsNullOrWhiteSpace(raw)
            ? "active"
            : raw.Trim().ToLowerInvariant();
    }
}
