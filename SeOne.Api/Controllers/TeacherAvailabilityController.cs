using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using System.Security.Claims;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/teachers")]
public class TeacherAvailabilityController : ControllerBase
{
    private readonly ITeacherAvailabilityService _availabilityService;
    private readonly ITeacherService _teacherService;

    public TeacherAvailabilityController(ITeacherAvailabilityService availabilityService, ITeacherService teacherService)
    {
        _availabilityService = availabilityService;
        _teacherService = teacherService;
    }

    [HttpGet("{teacherId:guid}/availability")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TeacherAvailabilityDto>>> GetByTeacher(Guid teacherId)
    {
        // ensure teacher exists and is a teacher
        var teacher = await _teacherService.GetTeacherByIdAsync(teacherId);

        if (teacher is null)
            return NotFound();

        var list = await _availabilityService.GetByTeacherAsync(teacherId);

        return Ok(list);
    }

    [HttpGet("me/availability")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<List<TeacherAvailabilityDto>>> GetMyAvailability()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var list = await _availabilityService.GetMyAvailabilityAsync(teacherId);

        return Ok(list);
    }

    [HttpPost("me/availability")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<TeacherAvailabilityDto>> CreateMyAvailability([FromBody] CreateTeacherAvailabilityRequest request)
    {
        if (request is null)
            return BadRequest();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        if (request.EndTime <= request.StartTime)
            return BadRequest("EndTime must be later than StartTime.");

        var result = await _availabilityService.CreateAsync(teacherId, request.DayOfWeek, request.StartTime, request.EndTime, request.IsAvailable);

        if (result is null)
            return BadRequest("Could not create availability (possible duplicate or invalid teacher).");

        return CreatedAtAction(nameof(GetMyAvailability), null, result);
    }

    [HttpPut("me/availability/{id:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<TeacherAvailabilityDto>> UpdateMyAvailability(Guid id, [FromBody] UpdateTeacherAvailabilityRequest request)
    {
        if (request is null)
            return BadRequest();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        if (request.EndTime <= request.StartTime)
            return BadRequest("EndTime must be later than StartTime.");

        var updated = await _availabilityService.UpdateAsync(id, teacherId, request.StartTime, request.EndTime, request.IsAvailable);

        if (updated is null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("me/availability/{id:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> DeleteMyAvailability(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var ok = await _availabilityService.DeleteAsync(id, teacherId);

        if (!ok)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("me/availability")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> DeleteAllMyAvailability()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        await _availabilityService.DeleteAllAsync(teacherId);

        return NoContent();
    }
}
