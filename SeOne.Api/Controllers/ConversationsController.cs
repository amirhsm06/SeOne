using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IMessagingService _messagingService;

    public ConversationsController(
        IMessagingService messagingService)
    {
        _messagingService =
            messagingService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ConversationDto>>>
        GetConversations()
    {
        var userId = GetUserId();

        return Ok(
            await _messagingService
                .GetConversationsAsync(userId));
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<ConversationDto>>>
        SearchConversations(
            [FromQuery] string query)
    {
        var userId = GetUserId();

        return Ok(
            await _messagingService
                .SearchConversationsAsync(
                    userId,
                    query));
    }

    [HttpPost]
    public async Task<ActionResult<ConversationDto>>
        CreateConversation(
            [FromBody] CreateConversationRequest request)
    {
        var userId = GetUserId();

        var result =
            await _messagingService
                .CreateConversationAsync(
                    userId,
                    request);

        if (result is null)
        {
            return BadRequest(new
            {
                message =
                    "Unable to create the conversation."
            });
        }

        return CreatedAtAction(
            nameof(GetConversation),
            new
            {
                conversationId = result.Id
            },
            result);
    }

    [HttpGet("{conversationId:guid}")]
    public async Task<ActionResult<ConversationDto>>
        GetConversation(
            Guid conversationId)
    {
        var userId = GetUserId();

        var result =
            await _messagingService
                .GetConversationAsync(
                    userId,
                    conversationId);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpGet("{conversationId:guid}/messages")]
    public async Task<ActionResult<List<MessageDto>>>
        GetMessages(
            Guid conversationId)
    {
        var userId = GetUserId();

        var result =
            await _messagingService
                .GetMessagesAsync(
                    userId,
                    conversationId);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost("{conversationId:guid}/messages")]
    public async Task<ActionResult<MessageDto>>
        SendMessage(
            Guid conversationId,
            [FromBody] SendMessageRequest request)
    {
        var userId = GetUserId();

        var result =
            await _messagingService
                .SendMessageAsync(
                    userId,
                    conversationId,
                    request);

        if (result is null)
        {
            return BadRequest(new
            {
                message =
                    "Unable to send the message."
            });
        }

        return Ok(result);
    }

    [HttpPatch("{conversationId:guid}/read")]
    public async Task<IActionResult>
        MarkConversationAsRead(
            Guid conversationId)
    {
        var userId = GetUserId();

        var result =
            await _messagingService
                .MarkConversationAsReadAsync(
                    userId,
                    conversationId);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("{conversationId:guid}/typing")]
    public async Task<ActionResult<List<TypingStatusDto>>>
        GetTypingStatus(
            Guid conversationId)
    {
        var userId = GetUserId();

        var result =
            await _messagingService
                .GetTypingStatusAsync(
                    userId,
                    conversationId);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost("{conversationId:guid}/typing")]
    public async Task<IActionResult>
        SetTypingStatus(
            Guid conversationId,
            [FromBody] SetTypingStatusRequest request)
    {
        var userId = GetUserId();

        var result =
            await _messagingService
                .SetTypingStatusAsync(
                    userId,
                    conversationId,
                    request.IsTyping);

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