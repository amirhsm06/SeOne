using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(
        IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpGet("course/{courseId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<ReviewDto>>> GetCourseReviews(
        Guid courseId)
    {
        var currentUserId = TryGetUserId();

        var result =
            await _reviewService.GetCourseReviewsAsync(
                courseId,
                currentUserId);

        if (result is null)
        {
            return NotFound(new
            {
                message = "Course not found or is not published."
            });
        }

        return Ok(result);
    }

    [HttpGet("course/{courseId:guid}/summary")]
    [AllowAnonymous]
    public async Task<ActionResult<CourseRatingSummaryDto>>
        GetCourseRatingSummary(Guid courseId)
    {
        var result =
            await _reviewService.GetCourseRatingSummaryAsync(
                courseId);

        if (result is null)
        {
            return NotFound(new
            {
                message = "Course not found or is not published."
            });
        }

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<ReviewDto>> Create(
        [FromBody] CreateReviewRequest request)
    {
        var studentId = GetRequiredUserId();

        var result =
            await _reviewService.CreateAsync(
                studentId,
                request.CourseId,
                request.Rating,
                request.Title,
                request.Content);

        if (result is null)
        {
            return BadRequest(new
            {
                message =
                    "You must be enrolled in the published course and must not already have a review for it."
            });
        }

        return Ok(result);
    }

    [HttpGet("user")]
    [Authorize]
    public async Task<ActionResult<List<ReviewDto>>> GetUserReviews()
    {
        var userId = GetRequiredUserId();

        var result =
            await _reviewService.GetUserReviewsAsync(
                userId);

        return Ok(result);
    }

    [HttpGet("{reviewId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ReviewDto>> GetById(
        Guid reviewId)
    {
        var currentUserId = TryGetUserId();

        var result =
            await _reviewService.GetByIdAsync(
                reviewId,
                currentUserId);

        if (result is null)
        {
            return NotFound(new
            {
                message = "Review not found."
            });
        }

        return Ok(result);
    }

    [HttpPatch("{reviewId:guid}")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<ReviewDto>> Update(
        Guid reviewId,
        [FromBody] UpdateReviewRequest request)
    {
        var studentId = GetRequiredUserId();

        var result =
            await _reviewService.UpdateAsync(
                reviewId,
                studentId,
                request.Rating,
                request.Title,
                request.Content);

        if (result is null)
        {
            return BadRequest(new
            {
                message = "Review not found or the supplied data is invalid."
            });
        }

        return Ok(result);
    }

    [HttpDelete("{reviewId:guid}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Delete(
        Guid reviewId)
    {
        var studentId = GetRequiredUserId();

        var deleted =
            await _reviewService.DeleteAsync(
                reviewId,
                studentId);

        if (!deleted)
        {
            return NotFound(new
            {
                message = "Review not found."
            });
        }

        return NoContent();
    }

    [HttpPost("{reviewId:guid}/helpful")]
    [Authorize]
    public async Task<IActionResult> MarkHelpful(
        Guid reviewId)
    {
        var userId = GetRequiredUserId();

        var result =
            await _reviewService.MarkHelpfulAsync(
                reviewId,
                userId);

        if (!result)
        {
            return NotFound(new
            {
                message = "Review not found."
            });
        }

        return NoContent();
    }

    [HttpDelete("{reviewId:guid}/helpful")]
    [Authorize]
    public async Task<IActionResult> UnmarkHelpful(
        Guid reviewId)
    {
        var userId = GetRequiredUserId();

        var result =
            await _reviewService.UnmarkHelpfulAsync(
                reviewId,
                userId);

        if (!result)
        {
            return NotFound(new
            {
                message = "Review not found."
            });
        }

        return NoContent();
    }

    private Guid GetRequiredUserId()
    {
        var claim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(claim, out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        return userId;
    }

    private Guid? TryGetUserId()
    {
        var claim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        return Guid.TryParse(claim, out var userId)
            ? userId
            : null;
    }
}