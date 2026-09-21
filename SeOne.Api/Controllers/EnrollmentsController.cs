using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using System.Security.Claims;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/enrollments")]
[Authorize]
public class EnrollmentsController : ControllerBase
{
    private readonly IEnrollmentService _enrollmentService;

    public EnrollmentsController(IEnrollmentService enrollmentService)
    {
        _enrollmentService = enrollmentService;
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<EnrollmentDto>> Enroll([FromBody] EnrollCourseRequest request)
    {
        if (request is null)
            return BadRequest();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var studentId))
            return Forbid();

        var result = await _enrollmentService.EnrollAsync(studentId, request.CourseId);

        if (result is null)
        {
            // determine reason: already enrolled or course missing/unpublished
            var already = await _enrollmentService.IsEnrolledAsync(studentId, request.CourseId);

            if (already)
                return BadRequest("Student is already enrolled in this course.");

            return BadRequest("Course not found or not published.");
        }

        return CreatedAtAction(nameof(GetMyEnrollments), null, result);
    }

    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<List<EnrollmentDto>>> GetMyEnrollments()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var studentId))
            return Forbid();

        var list = await _enrollmentService.GetMyEnrollmentsAsync(studentId);

        return Ok(list);
    }

    [HttpGet("{courseId:guid}/status")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<object>> GetStatus(Guid courseId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var studentId))
            return Forbid();

        var isEnrolled = await _enrollmentService.IsEnrolledAsync(studentId, courseId);

        return Ok(new { isEnrolled });
    }

    [HttpGet("course/{courseId:guid}/students")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<List<CourseEnrollmentStudentDto>>> GetCourseStudents(Guid courseId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var students = await _enrollmentService.GetCourseStudentsAsync(courseId, teacherId);

        return Ok(students);
    }
}
