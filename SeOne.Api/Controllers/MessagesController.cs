using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.Interfaces;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessagingService _messagingService;

    public MessagesController(
        IMessagingService messagingService)
    {
        _messagingService =
            messagingService;
    }

    [HttpPatch("{messageId:guid}/read")]
    public async Task<IActionResult>
        MarkAsRead(Guid messageId)
    {
        var userId = GetUserId();

        var result =
            await _messagingService
                .MarkMessageAsReadAsync(
                    userId,
                    messageId);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
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