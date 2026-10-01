using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.Interfaces;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/uploads")]
[Authorize]
public class UploadsController : ControllerBase
{
    private readonly IImageStorageService _imageStorage;

    public UploadsController(IImageStorageService imageStorage)
    {
        _imageStorage = imageStorage;
    }

    [HttpPost("course-image")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> UploadCourseImage(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Image file is required." });

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only image files are allowed." });

        if (file.Length > 6 * 1024 * 1024)
            return BadRequest(new { message = "Image must be 6 MB or smaller." });

        try
        {
            await using var stream = file.OpenReadStream();

            var url = await _imageStorage.UploadAsync(
                stream,
                file.FileName,
                file.ContentType,
                "courses",
                cancellationToken);

            return Ok(new
            {
                imageUrl = url
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPost("teacher-avatar")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> UploadTeacherAvatar(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Image file is required." });

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only image files are allowed." });

        if (file.Length > 6 * 1024 * 1024)
            return BadRequest(new { message = "Image must be 6 MB or smaller." });

        try
        {
            await using var stream = file.OpenReadStream();

            var url = await _imageStorage.UploadAsync(stream, file.FileName, file.ContentType, "teachers", cancellationToken);

            return Ok(new { imageUrl = url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("student-avatar")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [Authorize(Roles = "Admin,Student")]
    public async Task<IActionResult> UploadStudentAvatar(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Image file is required." });

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only image files are allowed." });

        if (file.Length > 6 * 1024 * 1024)
            return BadRequest(new { message = "Image must be 6 MB or smaller." });

        try
        {
            await using var stream = file.OpenReadStream();

            var url = await _imageStorage.UploadAsync(stream, file.FileName, file.ContentType, "students", cancellationToken);

            return Ok(new { imageUrl = url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("blog-image")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadBlogImage(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Image file is required." });

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only image files are allowed." });

        if (file.Length > 6 * 1024 * 1024)
            return BadRequest(new { message = "Image must be 6 MB or smaller." });

        try
        {
            await using var stream = file.OpenReadStream();

            var url = await _imageStorage.UploadAsync(stream, file.FileName, file.ContentType, "blog", cancellationToken);

            return Ok(new { imageUrl = url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}