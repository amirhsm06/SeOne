using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/student/submissions")]
[Authorize(Roles = "Student")]
public class StudentSubmissionsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public StudentSubmissionsController(
        IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    [HttpGet]
    public async Task<ActionResult<List<SubmissionDto>>>
        GetSubmissions()
    {
        var studentId = GetUserId();

        return Ok(
            await _assignmentService
                .GetStudentSubmissionsAsync(studentId));
    }

    [HttpGet("{submissionId:guid}")]
    public async Task<ActionResult<SubmissionDto>>
        GetSubmission(Guid submissionId)
    {
        var studentId = GetUserId();

        var result =
            await _assignmentService
                .GetStudentSubmissionAsync(
                    studentId,
                    submissionId);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPatch("{submissionId:guid}")]
    public async Task<ActionResult<SubmissionDto>>
        UpdateSubmission(
            Guid submissionId,
            [FromBody] UpdateSubmissionRequest request)
    {
        var studentId = GetUserId();

        var result =
            await _assignmentService
                .UpdateStudentSubmissionAsync(
                    studentId,
                    submissionId,
                    request);

        if (result is null)
        {
            return BadRequest(new
            {
                message =
                    "Submission not found, or only draft submissions can be edited."
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