using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/student/attachments")]
[Authorize(Roles = "Student")]
public sealed class StudentAttachmentsController : ApiControllerBase
{
    private readonly IAssignmentService _assignmentService;
    private readonly IAssignmentFileStorageService _assignmentFileStorage;

    public StudentAttachmentsController(
        IAssignmentService assignmentService,
        IAssignmentFileStorageService assignmentFileStorage)
    {
        _assignmentService = assignmentService;
        _assignmentFileStorage = assignmentFileStorage;
    }

    [HttpGet("download-url")]
    public async Task<IActionResult> GetDownloadUrl(
        [FromQuery] string? path,
        CancellationToken cancellationToken)
    {
        var studentId = RequireUserId();
        if (!_assignmentFileStorage.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Assignment file storage is unavailable." });

        if (!_assignmentFileStorage.TryNormalizeObjectPath(path, out var objectPath))
            return BadRequest(new { message = "The attachment path is invalid." });

        var assignments = await _assignmentService.GetStudentAssignmentsAsync(studentId);
        var submissions = await _assignmentService.GetStudentSubmissionsAsync(studentId);
        var isReferenced = assignments
            .SelectMany(assignment => assignment.Attachments)
            .Concat(submissions.SelectMany(submission => submission.Attachments))
            .Any(reference =>
                _assignmentFileStorage.TryNormalizeObjectPath(reference, out var referencedPath) &&
                string.Equals(referencedPath, objectPath, StringComparison.Ordinal));

        if (!isReferenced)
            return NotFound();

        var result = await _assignmentFileStorage.CreateSignedDownloadUrlAsync(objectPath, cancellationToken);
        return result.Outcome switch
        {
            AssignmentStorageOperationOutcome.Succeeded when result.Url is not null => Ok(new { url = result.Url }),
            AssignmentStorageOperationOutcome.InvalidRequest => BadRequest(new { message = "The attachment path is invalid." }),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Assignment file storage is unavailable." })
        };
    }
}
