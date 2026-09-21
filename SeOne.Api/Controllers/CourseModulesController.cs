using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using System.Security.Claims;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/courses/{courseId:guid}/modules")]
[Authorize]
public class CourseModulesController : ControllerBase
{
    private readonly ICourseModuleService _moduleService;
    private readonly ICourseService _courseService;
    private readonly IEnrollmentService _enrollmentService;

    public CourseModulesController(ICourseModuleService moduleService, ICourseService courseService, IEnrollmentService enrollmentService)
    {
        _moduleService = moduleService;
        _courseService = courseService;
        _enrollmentService = enrollmentService;
    }

    [HttpPost]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<CourseModuleDto>> Create(Guid courseId, [FromBody] CreateCourseModuleRequest request)
    {
        if (request is null)
            return BadRequest();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        // Verify course ownership
        var course = await _courseService.GetByIdAsync(courseId, teacherId, "Teacher");

        if (course is null)
            return NotFound();

        var created = await _moduleService.CreateAsync(teacherId, courseId, request.Title, request.Description, request.Order);

        if (created is null)
            return BadRequest();

        return CreatedAtAction(nameof(GetByCourse), new { courseId = courseId }, created);
    }

    [HttpGet]
    public async Task<ActionResult<List<CourseModuleDto>>> GetByCourse(Guid courseId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Forbid();

        // Use course service to validate visibility — returns null if not visible to this user
        var course = await _courseService.GetByIdAsync(courseId, userId, role);

        if (course is null)
            return NotFound();

        if (string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase))
        {
            var enrolled = await _enrollmentService.IsEnrolledAsync(userId, courseId);

            if (!enrolled)
                return NotFound();
        }

        var modules = await _moduleService.GetByCourseAsync(courseId, userId, role);

        return Ok(modules);
    }

    [HttpPut("{moduleId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<CourseModuleDto>> Update(Guid courseId, Guid moduleId, [FromBody] UpdateCourseModuleRequest request)
    {
        if (request is null)
            return BadRequest();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        // Verify module exists and belongs to course and teacher
        var updated = await _moduleService.UpdateAsync(moduleId, teacherId, request.Title, request.Description, request.Order);

        if (updated is null || updated.CourseId != courseId)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{moduleId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> Delete(Guid courseId, Guid moduleId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var ok = await _moduleService.DeleteAsync(moduleId, teacherId);

        if (!ok)
            return NotFound();

        return NoContent();
    }
}
