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

        if (objectPath.StartsWith(
        "student-assignments/",
        StringComparison.Ordinal))
        {
            var segments = objectPath.Split('/');

            if (segments.Length != 4 ||
                !Guid.TryParseExact(segments[1], "D", out var ownerId) ||
                ownerId != studentId ||
                !Guid.TryParseExact(segments[2], "D", out _))
            {
                return NotFound();
            }
        }

        var assignments = await _assignmentService.GetStudentAssignmentsAsync(studentId);
        var submissions = await _assignmentService.GetStudentSubmissionsAsync(studentId);

        bool IsReferenced(IEnumerable<string> references) =>
            references.Any(reference =>
                _assignmentFileStorage.TryNormalizeObjectPath(
                    reference, out var referencedPath) &&
                string.Equals(
                    referencedPath,
                    objectPath,
                    StringComparison.Ordinal));

        bool isReferenced;

        if (objectPath.StartsWith(
                "student-assignments/",
                StringComparison.Ordinal))
        {
            var segments = objectPath.Split('/');

            if (segments.Length != 4 ||
                segments[0] != "student-assignments" ||
                !Guid.TryParseExact(segments[1], "D", out var ownerId) ||
                ownerId != studentId ||
                !Guid.TryParseExact(segments[2], "D", out var pathAssignmentId))
            {
                return NotFound();
            }

            var fileName = segments[3];
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var fileId = Path.GetFileNameWithoutExtension(fileName);

            if (!Guid.TryParseExact(fileId, "N", out _) ||
                extension is not (
                    ".pdf" or ".docx" or ".jpg" or
                    ".jpeg" or ".png" or ".webp"))
            {
                return NotFound();
            }

            isReferenced = submissions.Any(submission =>
                submission.AssignmentId == pathAssignmentId &&
                IsReferenced(submission.Attachments));
        }
        else
        {
            // Non-student-upload paths are permitted only when referenced
            // by a teacher-created assignment available to this student.
            isReferenced = assignments.Any(assignment =>
                IsReferenced(assignment.Attachments));
        }

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
