using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/teacher/submissions")]
[Authorize(Roles = "Teacher")]
public class TeacherSubmissionsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public TeacherSubmissionsController(
        IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    [HttpPatch("{submissionId:guid}/grade")]
    public async Task<ActionResult<SubmissionDto>>
        GradeSubmission(
            Guid submissionId,
            [FromBody] GradeSubmissionRequest request)
    {
        var teacherId = GetUserId();

        var result =
            await _assignmentService
                .GradeSubmissionAsync(
                    teacherId,
                    submissionId,
                    request);

        if (result is null)
        {
            return BadRequest(new
            {
                message =
                    "Submission not found or grade is outside the assignment's maximum points."
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