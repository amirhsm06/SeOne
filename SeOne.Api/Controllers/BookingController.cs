using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using System.Security.Claims;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public class BookingController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<BookingDto>> Create([FromBody] CreateBookingRequest request)
    {
        if (request is null)
            return BadRequest();

        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var studentId))
            return Forbid();

        var created = await _bookingService.CreateAsync(studentId, request);

        if (created is null)
            return BadRequest();

        return CreatedAtAction(nameof(GetMyBookings), null, created);
    }

    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<List<BookingDto>>> GetMyBookings()
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var studentId))
            return Forbid();

        var list = await _bookingService.GetMyBookingsAsync(studentId);

        return Ok(list);
    }

    [HttpPatch("{id:guid}/cancel")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CancelMyBooking(Guid id)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var studentId))
            return Forbid();

        var ok = await _bookingService.CancelMyBookingAsync(id, studentId);

        if (!ok)
            return NotFound();

        return NoContent();
    }

    // Teacher endpoints
    [HttpGet("teacher")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<List<BookingDto>>> GetTeacherBookings()
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var list = await _bookingService.GetTeacherBookingsAsync(teacherId);

        return Ok(list);
    }

    [HttpPatch("{id:guid}/confirm")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var ok = await _bookingService.ConfirmBookingAsync(id, teacherId);

        if (!ok)
            return NotFound();

        return NoContent();
    }

    [HttpPatch("{id:guid}/teacher-cancel")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> CancelByTeacher(Guid id)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var ok = await _bookingService.CancelBookingByTeacherAsync(id, teacherId);

        if (!ok)
            return NotFound();

        return NoContent();
    }

    [HttpPatch("{id:guid}/complete")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> Complete(Guid id)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return Challenge();

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var teacherId))
            return Forbid();

        var ok = await _bookingService.CompleteBookingAsync(id, teacherId);

        if (!ok)
            return NotFound();

        return NoContent();
    }
}
