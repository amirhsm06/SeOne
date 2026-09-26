using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/teacher/assignments")]
[Authorize(Roles = "Teacher")]
public class TeacherAssignmentsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public TeacherAssignmentsController(
        IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AssignmentDto>>>
        GetAssignments()
    {
        var teacherId = GetUserId();

        return Ok(
            await _assignmentService
                .GetTeacherAssignmentsAsync(teacherId));
    }

    [HttpPost]
    public async Task<ActionResult<AssignmentDto>>
        CreateAssignment(
            [FromBody] CreateAssignmentRequest request)
    {
        var teacherId = GetUserId();

        var result =
            await _assignmentService
                .CreateAssignmentAsync(
                    teacherId,
                    request);

        if (result is null)
        {
            return BadRequest(new
            {
                message =
                    "Invalid assignment data or the course/module does not belong to you."
            });
        }

        return CreatedAtAction(
            nameof(GetAssignment),
            new
            {
                assignmentId = result.Id
            },
            result);
    }

    [HttpGet("{assignmentId:guid}")]
    public async Task<ActionResult<AssignmentDto>>
        GetAssignment(Guid assignmentId)
    {
        var teacherId = GetUserId();

        var result =
            await _assignmentService
                .GetTeacherAssignmentAsync(
                    teacherId,
                    assignmentId);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPatch("{assignmentId:guid}")]
    public async Task<ActionResult<AssignmentDto>>
        UpdateAssignment(
            Guid assignmentId,
            [FromBody] UpdateAssignmentRequest request)
    {
        var teacherId = GetUserId();

        var result =
            await _assignmentService
                .UpdateAssignmentAsync(
                    teacherId,
                    assignmentId,
                    request);

        if (result is null)
        {
            return BadRequest(new
            {
                message =
                    "Assignment not found or invalid data."
            });
        }

        return Ok(result);
    }

    [HttpDelete("{assignmentId:guid}")]
    public async Task<IActionResult>
        DeleteAssignment(Guid assignmentId)
    {
        var teacherId = GetUserId();

        var deleted =
            await _assignmentService
                .DeleteAssignmentAsync(
                    teacherId,
                    assignmentId);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("{assignmentId:guid}/submissions")]
    public async Task<ActionResult<List<SubmissionDto>>>
        GetSubmissions(Guid assignmentId)
    {
        var teacherId = GetUserId();

        var result =
            await _assignmentService
                .GetAssignmentSubmissionsAsync(
                    teacherId,
                    assignmentId);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    private Guid GetUserId()
    {
        var claim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(
                claim,
                out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        return userId;
    }
}