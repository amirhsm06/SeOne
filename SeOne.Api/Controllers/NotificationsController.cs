using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(
        INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>>
        GetNotifications(
            [FromQuery] string? type = null,
            [FromQuery] bool unreadOnly = false,
            [FromQuery] int? limit = null,
            [FromQuery] int? offset = null)
    {
        var userId = GetUserId();

        var result =
            await _notificationService.GetAsync(
                userId,
                type,
                unreadOnly,
                limit,
                offset);

        return Ok(result);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<NotificationStatsDto>>
        GetStats()
    {
        var userId = GetUserId();

        var result =
            await _notificationService.GetStatsAsync(
                userId);

        return Ok(result);
    }

    [HttpGet("{notificationId:guid}")]
    public async Task<ActionResult<NotificationDto>>
        GetNotification(
            Guid notificationId)
    {
        var userId = GetUserId();

        var result =
            await _notificationService.GetByIdAsync(
                userId,
                notificationId);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPatch("{notificationId:guid}/read")]
    public async Task<IActionResult>
        MarkAsRead(Guid notificationId)
    {
        var userId = GetUserId();

        var result =
            await _notificationService.MarkAsReadAsync(
                userId,
                notificationId);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult>
        MarkAllAsRead()
    {
        var userId = GetUserId();

        await _notificationService
            .MarkAllAsReadAsync(userId);

        return NoContent();
    }

    [HttpDelete("{notificationId:guid}")]
    public async Task<IActionResult>
        Delete(Guid notificationId)
    {
        var userId = GetUserId();

        var result =
            await _notificationService.DeleteAsync(
                userId,
                notificationId);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpDelete("clear")]
    public async Task<IActionResult>
        Clear(
            [FromQuery] bool readOnly = false,
            [FromQuery] DateTime? olderThan = null)
    {
        var userId = GetUserId();

        await _notificationService.ClearAsync(
            userId,
            readOnly,
            olderThan);

        return NoContent();
    }

    [HttpPost("{notificationId:guid}/action")]
    public async Task<ActionResult<NotificationActionResultDto>>
        ExecuteAction(
            Guid notificationId,
            [FromBody] Dictionary<string, object?>? actionData)
    {
        var userId = GetUserId();

        var result =
            await _notificationService
                .ExecuteActionAsync(
                    userId,
                    notificationId,
                    actionData);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost("test")]
    public async Task<ActionResult<NotificationDto>>
        CreateTestNotification(
            [FromBody]
            CreateTestNotificationRequest request)
    {
        var userId = GetUserId();

        var result =
            await _notificationService
                .CreateTestAsync(
                    userId,
                    request.Type);

        if (result is null)
        {
            return BadRequest(new
            {
                message = "Invalid notification type."
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