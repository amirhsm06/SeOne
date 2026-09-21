using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using System.Security.Claims;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/courses/{courseId:guid}/learning")]
[Authorize(Roles = "Student")]
public class CourseLearningController : ControllerBase
{
    private readonly ICourseLearningService _courseLearningService;

    public CourseLearningController(ICourseLearningService courseLearningService)
    {
        _courseLearningService = courseLearningService;
    }

    [HttpGet]
    public async Task<ActionResult<CourseLearningDto>> GetCourseLearning(Guid courseId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var studentId))
            return Forbid();

        var dto = await _courseLearningService.GetCourseLearningAsync(studentId, courseId);

        if (dto is null)
            return NotFound();

        return Ok(dto);
    }
}
