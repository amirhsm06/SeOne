using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using System.Security.Claims;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/courses")]
[Authorize]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;

    public CoursesController(ICourseService courseService)
    {
        _courseService = courseService;
    }

    [HttpPatch("{id:guid}/unpublish")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<CourseDto>> Unpublish(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var ok = await _courseService.SetPublishedAsync(id, teacherId, false);

        if (!ok)
            return NotFound();

        var course = await _courseService.GetByIdAsync(id, teacherId, "Teacher");

        if (course is null)
            return NotFound();

        return Ok(course);
    }

    [HttpPatch("{id:guid}/publish")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<CourseDto>> SetPublish(Guid id, [FromBody] PublishCourseRequest request)
    {
        if (request is null)
            return BadRequest();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var ok = await _courseService.SetPublishedAsync(id, teacherId, request.IsPublished);

        if (!ok)
            return NotFound();

        // reuse GetByIdAsync to return the updated course (visibility enforced)
        var course = await _courseService.GetByIdAsync(id, teacherId, "Teacher");

        if (course is null)
            return NotFound();

        return Ok(course);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<CourseDto>> Update(Guid id, [FromBody] UpdateCourseRequest request)
    {
        if (request is null)
            return BadRequest();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var updated = await _courseService.UpdateAsync(id, teacherId, request.Title, request.Description, request.Level, request.Price, request.Duration, request.ImageUrl);

        if (updated is null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var ok = await _courseService.DeleteAsync(id, teacherId);

        if (!ok)
            return NotFound();

        return NoContent();
    }

    [HttpPost]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<CourseDto>> Create([FromBody] CreateCourseRequest request)
    {
        if (request is null)
            return BadRequest();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var created = await _courseService.CreateAsync(request.Title, request.Description, request.Level, request.Price, request.Duration, request.ImageUrl, teacherId);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<CourseCatalogDto>>> GetAll(
    [FromQuery] string lang = "en")
    {
        var catalog = await _courseService.GetPublishedCatalogAsync(lang);

        return Ok(catalog);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CourseDto>> GetById(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Forbid();

        var course = await _courseService.GetByIdAsync(id, userId, role);

        if (course is null)
            return NotFound();

        return Ok(course);
    }
}