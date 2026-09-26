using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/wishlist")]
[Authorize(Roles = "Student")]
public class WishlistController : ControllerBase
{
    private readonly IWishlistService _wishlistService;

    public WishlistController(
        IWishlistService wishlistService)
    {
        _wishlistService = wishlistService;
    }

    [HttpGet]
    public async Task<ActionResult<List<WishlistItemDto>>> Get()
    {
        var studentId = GetStudentId();

        var result =
            await _wishlistService.GetAsync(studentId);

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<WishlistItemDto>> Add(
        [FromBody] AddWishlistItemRequest request)
    {
        var studentId = GetStudentId();

        var result =
            await _wishlistService.AddAsync(
                studentId,
                request.CourseId);

        if (result is null)
        {
            return NotFound(new
            {
                message = "Course not found or is not published."
            });
        }

        return Ok(result);
    }

    [HttpDelete("{courseId:guid}")]
    public async Task<IActionResult> Remove(
        Guid courseId)
    {
        var studentId = GetStudentId();

        var removed =
            await _wishlistService.RemoveAsync(
                studentId,
                courseId);

        if (!removed)
        {
            return NotFound(new
            {
                message = "Course is not in your wishlist."
            });
        }

        return NoContent();
    }

    [HttpGet("check/{courseId:guid}")]
    public async Task<IActionResult> Check(
        Guid courseId)
    {
        var studentId = GetStudentId();

        var exists =
            await _wishlistService.ExistsAsync(
                studentId,
                courseId);

        return Ok(new
        {
            exists
        });
    }

    private Guid GetStudentId()
    {
        var claim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(claim, out var studentId))
        {
            throw new UnauthorizedAccessException();
        }

        return studentId;
    }
}