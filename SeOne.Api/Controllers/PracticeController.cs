using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/practice")]
[Authorize]
public class PracticeController : ControllerBase
{
    private readonly IPracticeService _practiceService;

    public PracticeController(IPracticeService practiceService)
    {
        _practiceService = practiceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPracticeContent(
        [FromQuery] Guid? courseId = null,
        [FromQuery] Guid? moduleId = null,
        [FromQuery] Guid? lessonId = null,
        [FromQuery] string? language = null,
        [FromQuery] string? level = null,
        [FromQuery] string? category = null)
    {
        var userId = GetUserId();
        var role = GetRole();

        if (string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(await _practiceService.GetTeacherPracticeContentAsync(
                userId,
                courseId,
                moduleId,
                lessonId,
                language,
                level,
                category));
        }

        if (string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(await _practiceService.GetStudentPracticeContentAsync(
                userId,
                courseId,
                moduleId,
                lessonId,
                language,
                level,
                category));
        }

        return Forbid();
    }

    [HttpGet("{practiceId:guid}")]
    public async Task<IActionResult> GetPractice(Guid practiceId)
    {
        var userId = GetUserId();
        var role = GetRole();

        if (string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase))
        {
            var practice = await _practiceService
                .GetTeacherPracticeAsync(userId, practiceId);

            return practice is null
                ? NotFound()
                : Ok(practice);
        }

        if (string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase))
        {
            var practice = await _practiceService
                .GetStudentPracticeAsync(userId, practiceId);

            return practice is null
                ? NotFound()
                : Ok(practice);
        }

        return Forbid();
    }

    [HttpPost]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<PracticeContentDto>> CreatePractice(
        [FromBody] CreatePracticeRequest request)
    {
        if (request is null)
            return BadRequest();

        var teacherId = GetUserId();

        var created = await _practiceService.CreatePracticeAsync(
            teacherId,
            request);

        if (created is null)
        {
            return BadRequest(new
            {
                message =
                    "Invalid practice data, question data, or the course/module/lesson does not belong to you."
            });
        }

        return CreatedAtAction(
            nameof(GetPractice),
            new { practiceId = created.Id },
            created);
    }

    [HttpPatch("{practiceId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<PracticeContentDto>> UpdatePractice(
        Guid practiceId,
        [FromBody] UpdatePracticeRequest request)
    {
        if (request is null)
            return BadRequest();

        var teacherId = GetUserId();

        var updated = await _practiceService.UpdatePracticeAsync(
            teacherId,
            practiceId,
            request);

        if (updated is null)
        {
            return BadRequest(new
            {
                message =
                    "Practice not found, the updated data is invalid, or questions cannot be replaced after an attempt exists."
            });
        }

        return Ok(updated);
    }

    [HttpDelete("{practiceId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> DeletePractice(Guid practiceId)
    {
        var teacherId = GetUserId();

        var deleted = await _practiceService.DeletePracticeAsync(
            teacherId,
            practiceId);

        return deleted
            ? NoContent()
            : NotFound();
    }

    [HttpPost("attempts")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<StartPracticeAttemptDto>> StartAttempt(
        [FromBody] StartPracticeAttemptRequest request)
    {
        if (request is null)
            return BadRequest();

        var studentId = GetUserId();

        var result = await _practiceService.StartAttemptAsync(
            studentId,
            request.PracticeId);

        if (result is null)
        {
            return BadRequest(new
            {
                message =
                    "The practice is unavailable, unpublished, or you are not enrolled in its course."
            });
        }

        return Ok(result);
    }

    [HttpPost("attempts/{attemptId:guid}/submit")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<PracticeAttemptDto>> SubmitAttempt(
        Guid attemptId,
        [FromBody] SubmitPracticeAttemptRequest request)
    {
        if (request is null)
            return BadRequest();

        var studentId = GetUserId();

        var result = await _practiceService.SubmitAttemptAsync(
            studentId,
            attemptId,
            request);

        if (result is null)
        {
            return BadRequest(new
            {
                message =
                    "The attempt does not belong to you, has already been submitted, or contains invalid question answers."
            });
        }

        return Ok(result);
    }

    [HttpGet("attempts")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<List<PracticeAttemptDto>>> GetAttempts(
        [FromQuery] Guid? practiceId = null)
    {
        var studentId = GetUserId();

        return Ok(await _practiceService.GetPracticeAttemptsAsync(
            studentId,
            practiceId));
    }

    [HttpGet("attempts/{attemptId:guid}")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<PracticeAttemptDto>> GetAttempt(Guid attemptId)
    {
        var studentId = GetUserId();

        var attempt = await _practiceService.GetAttemptAsync(
            studentId,
            attemptId);

        return attempt is null
            ? NotFound()
            : Ok(attempt);
    }

    [HttpGet("history")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<List<PracticeHistoryDto>>> GetHistory()
    {
        var studentId = GetUserId();

        return Ok(await _practiceService.GetHistoryAsync(studentId));
    }

    [HttpGet("streak")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<StreakInfoDto>> GetStreak()
    {
        var studentId = GetUserId();

        return Ok(await _practiceService.GetStreakAsync(studentId));
    }

    [HttpGet("leaderboard/{practiceId:guid}")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<List<PracticeLeaderboardEntryDto>>> GetLeaderboard(
        Guid practiceId)
    {
        var studentId = GetUserId();

        var leaderboard = await _practiceService.GetLeaderboardAsync(
            studentId,
            practiceId);

        return leaderboard is null
            ? NotFound()
            : Ok(leaderboard);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(claim, out var userId))
            throw new UnauthorizedAccessException();

        return userId;
    }

    private string GetRole()
    {
        return User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    }
}
