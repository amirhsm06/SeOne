using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using System.Security.Claims;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/progress")]
[Authorize(Roles = "Student")]
public class ProgressController : ControllerBase
{
    private readonly IProgressService _progressService;

    public ProgressController(IProgressService progressService)
    {
        _progressService = progressService;
    }

    [HttpPut("lessons/{lessonId:guid}")]
    public async Task<ActionResult<LessonProgressDto>> UpdateLessonProgress(Guid lessonId, [FromBody] UpdateLessonProgressRequest request)
    {
        if (request is null)
            return BadRequest();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var studentId))
            return Forbid();

        var result = await _progressService.UpdateLessonProgressAsync(studentId, lessonId, request.IsCompleted);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("lessons/{lessonId:guid}")]
    public async Task<ActionResult<LessonProgressDto>> GetLessonProgress(Guid lessonId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var studentId))
            return Forbid();

        var result = await _progressService.GetLessonProgressAsync(studentId, lessonId);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("courses/{courseId:guid}")]
    public async Task<ActionResult<CourseProgressDto>> GetCourseProgress(Guid courseId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var studentId))
            return Forbid();

        var result = await _progressService.GetCourseProgressAsync(studentId, courseId);

        return Ok(result);
    }

    [HttpGet("modules/{moduleId:guid}")]
    public async Task<ActionResult<ModuleProgressDto>> GetModuleProgress(Guid moduleId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var studentId))
            return Forbid();

        var result = await _progressService.GetModuleProgressAsync(studentId, moduleId);

        return Ok(result);
    }
}
