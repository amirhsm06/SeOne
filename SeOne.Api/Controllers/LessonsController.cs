using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
// using SeOne.Infrastructure.Persistence;
// using SeOne.Domain.Entities;
using System.Security.Claims;
// Keep Microsoft.EntityFrameworkCore import to prevent analyzers from removing EF Core references.

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/modules/{courseModuleId:guid}/lessons")]
[Authorize]
public class LessonsController : ControllerBase
{
    private readonly ILessonService _lessonService;

    public LessonsController(ILessonService lessonService)
    {
        _lessonService = lessonService;
    }

    [HttpPost]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<LessonDto>> Create(Guid courseModuleId, [FromBody] CreateLessonRequest request)
    {
        if (request is null)
            return BadRequest();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var result = await _lessonService.CreateAsync(teacherId, courseModuleId, request.Title, request.Content, request.Order);

        if (result is null)
            return NotFound();

        return CreatedAtAction(nameof(GetByModule), new { courseModuleId = courseModuleId }, result);
    }

    [HttpGet]
    public async Task<ActionResult<List<LessonDto>>> GetByModule(Guid courseModuleId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Forbid();

        // Delegate visibility and enrollment checks to the lesson service
        var lessons = await _lessonService.GetByModuleAsync(courseModuleId, userId, role);

        // Service returns null when module not found or access denied -> map to 404
        if (lessons is null)
            return NotFound();

        // Otherwise return lessons (may be empty) with 200
        return Ok(lessons);
    }

    [HttpPut("{lessonId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<LessonDto>> Update(Guid courseModuleId, Guid lessonId, [FromBody] UpdateLessonRequest request)
    {
        if (request is null)
            return BadRequest();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        // Delegate existence/ownership checks to service
        var updated = await _lessonService.UpdateAsync(courseModuleId, lessonId, teacherId, request.Title, request.Content, request.Order);

        if (updated is null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{lessonId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> Delete(Guid courseModuleId, Guid lessonId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        // Delegate existence/ownership checks to service
        var ok = await _lessonService.DeleteAsync(courseModuleId, lessonId, teacherId);

        if (!ok)
            return NotFound();

        return NoContent();
    }
}
