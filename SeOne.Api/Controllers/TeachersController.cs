using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using System.Security.Claims;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/teachers")]
public class TeachersController : ControllerBase
{
    private readonly ITeacherService _teacherService;
    private readonly ICourseService _courseService;
    private readonly ITeacherVideoStorageService _videoStorage;
    private readonly ILogger<TeachersController> _logger;

    public TeachersController(
        ITeacherService teacherService,
        ICourseService courseService,
        ITeacherVideoStorageService videoStorage,
        ILogger<TeachersController> logger)
    {
        _teacherService = teacherService;
        _courseService = courseService;
        _videoStorage = videoStorage;
        _logger = logger;
    }

    [HttpGet("me/courses/{courseId:guid}/students")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<List<TeacherCourseStudentDto>>> GetCourseStudents(Guid courseId, [FromServices] ITeacherCourseStudentService studentService)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var list = await studentService.GetStudentsForCourseAsync(teacherId, courseId);

        if (list == null || list.Count == 0)
        {
            // Either course not found/owned or no students. Need to distinguish: service returns empty list for not-owned course as well.
            // Verify ownership to decide 404 vs empty list
            var courseOwned = await _courseService.GetByIdAsync(courseId, teacherId, "Teacher") != null;

            if (!courseOwned)
                return NotFound();
        }

        return Ok(list);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<TeacherDto>>> GetAll()
    {
        var list = await _teacherService.GetAllTeachersAsync();
        return Ok(list);
    }

    [HttpGet("{teacherId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<TeacherDto>> GetById(Guid teacherId)
    {
        var dto = await _teacherService.GetTeacherByIdAsync(teacherId);

        if (dto is null)
            return NotFound();

        return Ok(dto);
    }

    [HttpGet("me")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<TeacherDto>> GetMyProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var dto = await _teacherService.GetMyProfileAsync(teacherId);

        if (dto is null)
            return NotFound();

        return Ok(dto);
    }

    [HttpPut("me")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<TeacherDto>> UpdateMyProfile([FromBody] UpdateTeacherProfileRequest request)
    {
        if (request is null)
            return BadRequest();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var updated = await _teacherService.UpdateMyProfileAsync(teacherId, request.AvatarUrl ?? request.Avatar, request.VideoUrl, request.TeachingLanguage, request.Subject, request.Level, request.Bio);

        if (updated is null)
            return NotFound();

        return Ok(updated);
    }
    [HttpPost("~/api/teacher/profile/video")]
    [Authorize(Roles = "Teacher")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadProfileVideo(
    IFormFile? video,
    IFormFile? file,
    CancellationToken cancellationToken)
    {
        var teacherIdValue =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(teacherIdValue, out var teacherId))
            return Forbid();

        var uploadedFile = video ?? file;

        if (uploadedFile is null || uploadedFile.Length == 0)
        {
            return BadRequest(new
            {
                message = "A video file is required."
            });
        }

        if (uploadedFile.Length > 45_000_000L)
        {
            return BadRequest(new
            {
                message = "Video size must not exceed 45 MB."
            });
        }

        var extension =
            Path.GetExtension(uploadedFile.FileName).ToLowerInvariant();

        var expectedContentType = extension switch
        {
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            _ => null
        };

        if (expectedContentType is null ||
            !string.Equals(
                expectedContentType,
                uploadedFile.ContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Only MP4 and WebM videos with matching content types are allowed."
            });
        }

        if (!_videoStorage.IsConfigured)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Teacher video storage is unavailable." });
        }

        var teacher = await _teacherService.GetMyProfileAsync(teacherId);

        if (teacher is null)
            return NotFound();

        string videoUrl;

        // Upload-stage errors are separate from database persistence errors.
        try
        {
            await using var stream = uploadedFile.OpenReadStream();

            videoUrl = await _videoStorage.UploadAsync(
                teacherId,
                stream,
                uploadedFile.FileName,
                uploadedFile.ContentType,
                uploadedFile.Length,
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new
            {
                message = "The video was rejected by storage validation."
            });
        }
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Teacher video storage is unavailable." });
        }

        // The object now exists. If persistence fails, clean it up only after
        // verifying that the teacher profile does not reference this URL.
        try
        {
            var updated = await _teacherService.UpdateMyVideoUrlAsync(
                teacherId,
                videoUrl);

            if (!updated)
            {
                await TryDeleteUploadedVideoIfUnreferencedAsync(
                    teacherId,
                    videoUrl);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message = "The video was uploaded but the profile could not be updated."
                    });
            }

            return Ok(new { videoUrl });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to save the video URL for teacher {TeacherId}.",
                teacherId);

            await TryDeleteUploadedVideoIfUnreferencedAsync(
                teacherId,
                videoUrl);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "The video was uploaded but the profile could not be updated."
                });
        }
    }

    private async Task<bool> TryDeleteUploadedVideoIfUnreferencedAsync(
        Guid teacherId,
        string videoUrl)
    {
        try
        {
            // Re-read the current profile because a failed database operation
            // can have an ambiguous outcome.
            var currentProfile =
                await _teacherService.GetMyProfileAsync(teacherId);

            if (currentProfile is null)
            {
                _logger.LogWarning(
                    "Skipped video cleanup because the teacher profile could not be verified for teacher {TeacherId}.",
                    teacherId);

                return false;
            }

            if (string.Equals(
                    currentProfile.VideoUrl,
                    videoUrl,
                    StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Skipped video cleanup because the teacher profile still references the uploaded video for teacher {TeacherId}.",
                    teacherId);

                return false;
            }

            var deleted = await _videoStorage.DeleteAsync(
                teacherId,
                videoUrl,
                CancellationToken.None);

            if (!deleted)
            {
                _logger.LogWarning(
                    "Could not delete the unreferenced uploaded video for teacher {TeacherId}.",
                    teacherId);
            }

            return deleted;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "An error occurred while cleaning up an unreferenced uploaded video for teacher {TeacherId}.",
                teacherId);

            return false;
        }
    }
}
