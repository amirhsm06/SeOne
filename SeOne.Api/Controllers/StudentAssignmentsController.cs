using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/student/assignments")]
[Authorize(Roles = "Student")]
public class StudentAssignmentsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public StudentAssignmentsController(
        IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AssignmentDto>>>
        GetAssignments()
    {
        var studentId = GetUserId();

        return Ok(
            await _assignmentService
                .GetStudentAssignmentsAsync(studentId));
    }

    [HttpGet("{assignmentId:guid}")]
    public async Task<ActionResult<StudentAssignmentDto>>
        GetAssignment(Guid assignmentId)
    {
        var studentId = GetUserId();

        var result =
            await _assignmentService
                .GetStudentAssignmentAsync(
                    studentId,
                    assignmentId);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost("{assignmentId:guid}/submissions")]
    public async Task<ActionResult<SubmissionDto>>
        SubmitAssignment(
            Guid assignmentId,
            [FromBody] CreateSubmissionRequest request)
    {
        var studentId = GetUserId();

        var result =
            await _assignmentService
                .SubmitAssignmentAsync(
                    studentId,
                    assignmentId,
                    request);

        if (result is null)
        {
            return BadRequest(new
            {
                message =
                    "The assignment is unavailable, you are not enrolled, or you already submitted it."
            });
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