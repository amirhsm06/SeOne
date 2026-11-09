using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/student/assignments")]
[Authorize(Roles = "Student")]
public class StudentAssignmentsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;
    private readonly IAssignmentFileStorageService _assignmentFileStorage;

    public StudentAssignmentsController(
        IAssignmentService assignmentService,
        IAssignmentFileStorageService assignmentFileStorage)
    {
        _assignmentService = assignmentService;
        _assignmentFileStorage = assignmentFileStorage;
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

    [HttpPut("{assignmentId:guid}/submissions/draft")]
    public async Task<IActionResult> SaveDraft(
        Guid assignmentId,
        [FromBody] CreateSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _assignmentService.SaveDraftAsync(
            GetUserId(),
            assignmentId,
            request,
            cancellationToken);

        return result.Outcome switch
        {
            SubmissionOperationOutcome.Succeeded when result.Submission is not null => Ok(result.Submission),
            SubmissionOperationOutcome.InvalidRequest => BadRequest(new { message = "Submission content or attachments are invalid." }),
            SubmissionOperationOutcome.AssignmentUnavailable => NotFound(new { message = "Assignment is not available to this student." }),
            SubmissionOperationOutcome.Conflict => Conflict(new { message = "A submission exists and its current status does not permit draft editing." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{assignmentId:guid}/complete")]
    public async Task<IActionResult> CompleteAssignment(
        Guid assignmentId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateSubmissionRequest? request,
        CancellationToken cancellationToken)
    {
        var studentId = GetUserId();
        var assignment = await _assignmentService.GetStudentAssignmentAsync(studentId, assignmentId);
        if (assignment is null)
            return NotFound(new { message = "Assignment is not available to this student." });

        SubmissionOperationResult result;
        var existingSubmission = assignment.Submission;
        if (existingSubmission is null)
        {
            if (request is null)
                return BadRequest(new { message = "Submission content or attachments are required." });

            var draftResult = await _assignmentService.SaveDraftAsync(
                studentId,
                assignmentId,
                request,
                cancellationToken);
            if (draftResult.Outcome != SubmissionOperationOutcome.Succeeded || draftResult.Submission is null)
                return MapCompletionResult(draftResult);

            existingSubmission = draftResult.Submission;
        }
        else if (existingSubmission.Status is not ("draft" or "returned"))
        {
            return Conflict(new { message = "This assignment submission cannot be completed in its current state." });
        }

        request ??= new CreateSubmissionRequest
        {
            Content = existingSubmission.Content,
            Attachments = existingSubmission.Attachments
        };

        result = await _assignmentService.FinalizeSubmissionAsync(
            studentId,
            existingSubmission.Id,
            request,
            cancellationToken);

        return MapCompletionResult(result);
    }

    [HttpPost("~/api/student/submissions/{submissionId:guid}/submit")]
    public async Task<IActionResult> FinalizeSubmission(
        Guid submissionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateSubmissionRequest? request,
        CancellationToken cancellationToken)
    {
        var studentId = GetUserId();
        var submission = await _assignmentService.GetStudentSubmissionAsync(studentId, submissionId);
        if (submission is null)
            return NotFound(new { message = "Submission was not found." });

        request ??= new CreateSubmissionRequest
        {
            Content = submission.Content,
            Attachments = submission.Attachments
        };

        var result = await _assignmentService.FinalizeSubmissionAsync(
            studentId,
            submissionId,
            request,
            cancellationToken);

        return MapCompletionResult(result);
    }

    [HttpPost("{assignmentId:guid}/submissions/upload-grant")]
    public async Task<IActionResult> CreateSubmissionUploadGrant(
        Guid assignmentId,
        [FromBody] AssignmentUploadGrantRequest request,
        CancellationToken cancellationToken)
    {
        var studentId = GetUserId();
        if (await _assignmentService.GetStudentAssignmentAsync(studentId, assignmentId) is null)
            return NotFound(new { message = "Assignment is not available to this student." });

        if (!_assignmentFileStorage.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Assignment file storage is unavailable." });

        var result = await _assignmentFileStorage.CreateUploadGrantAsync(
            studentId,
            assignmentId,
            request,
            cancellationToken);

        return result.Outcome switch
        {
            AssignmentStorageOperationOutcome.Succeeded when result.Grant is not null => Ok(result.Grant),
            AssignmentStorageOperationOutcome.InvalidRequest => BadRequest(new { message = "The file name, type, or size is not allowed." }),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Assignment file storage is unavailable." })
        };
    }

    private IActionResult MapCompletionResult(SubmissionOperationResult result) => result.Outcome switch
    {
        SubmissionOperationOutcome.Succeeded when result.Submission is not null => Ok(result.Submission),
        SubmissionOperationOutcome.InvalidRequest => BadRequest(new { message = "Submission content or attachments are invalid." }),
        SubmissionOperationOutcome.AssignmentUnavailable => NotFound(new { message = "Assignment is not available to this student." }),
        SubmissionOperationOutcome.SubmissionNotFound => NotFound(new { message = "Submission was not found." }),
        SubmissionOperationOutcome.Conflict => Conflict(new { message = "A submission exists and its current status does not permit completion." }),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };

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