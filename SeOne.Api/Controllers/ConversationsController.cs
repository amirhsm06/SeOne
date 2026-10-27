using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IMessagingService _messagingService;
    private readonly SeOneDbContext _db;
    private readonly IMessagingFileStorageService _messagingFileStorage;
    private readonly ILogger<ConversationsController> _logger;

    public ConversationsController(
        IMessagingService messagingService,
        SeOneDbContext db,
        IMessagingFileStorageService messagingFileStorage,
        ILogger<ConversationsController> logger)
    {
        _messagingService = messagingService;
        _db = db;
        _messagingFileStorage = messagingFileStorage;
        _logger = logger;
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

    [HttpGet("contacts")]
    public async Task<ActionResult<List<MessagingContactDto>>> GetContacts()
    {
        var userId = GetUserId();

        return Ok(await _messagingService.GetMessagingContactsAsync(userId));
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

    [HttpPost("{conversationId:guid}/messages/{messageId:guid}/upload")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<IActionResult> UploadMessageAttachment(
    Guid conversationId,
    Guid messageId,
    IFormFile? attachment,
    CancellationToken cancellationToken)
    {
        const long maximumFileSize = 20 * 1024 * 1024;
        const int maximumAttachments = 5;

        var userId = GetUserId();

        if (attachment is null || attachment.Length <= 0)
            return BadRequest(new { message = "Attachment file is required." });

        if (attachment.Length > maximumFileSize)
            return BadRequest(new { message = "Attachment must be 20 MiB or smaller." });

        var allowedExtensions = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".gif",
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt",
        ".mp3", ".m4a", ".wav", ".mp4", ".zip"
    };

        var extension = Path.GetExtension(attachment.FileName);

        if (string.IsNullOrWhiteSpace(extension) ||
            !allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "File type is not allowed." });
        }

        var participant = await _db.Set<ConversationParticipant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.ConversationId == conversationId && p.UserId == userId,
                cancellationToken);

        if (participant is null)
            return NotFound(new { message = "Conversation not found or access denied." });

        var message = await _db.Set<Message>()
            .FirstOrDefaultAsync(
                m => m.Id == messageId && m.ConversationId == conversationId,
                cancellationToken);

        if (message is null)
            return NotFound(new { message = "Message not found in the specified conversation." });

        if (message.SenderId != userId)
            return Forbid();

        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);

        var messageForUpdate = await _db.Set<Message>()
            .FirstOrDefaultAsync(
                m => m.Id == messageId && m.ConversationId == conversationId,
                cancellationToken);

        if (messageForUpdate is null)
            return NotFound(new { message = "Message not found." });

        if (messageForUpdate.SenderId != userId)
            return Forbid();

        if (messageForUpdate.Attachments.Count >= maximumAttachments)
            return BadRequest(new { message = "Maximum 5 attachments per message." });

        var objectPath =
            $"private-messaging/{conversationId:D}/{messageId:D}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

        bool uploaded;

        try
        {
            uploaded = await _messagingFileStorage.UploadAsync(
                objectPath,
                attachment.OpenReadStream(),
                attachment.ContentType,
                attachment.Length,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Messaging attachment upload failed for conversation {ConversationId} and message {MessageId}.",
                conversationId,
                messageId);

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Storage service unavailable." });
        }

        if (!uploaded)
        {
            var cleanupSucceeded = await _messagingFileStorage.DeleteObjectAsync(
                objectPath,
                CancellationToken.None);

            if (!cleanupSucceeded)
            {
                _logger.LogWarning(
                    "Cleanup after unsuccessful messaging upload failed for conversation {ConversationId} and message {MessageId}.",
                    conversationId,
                    messageId);
            }

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Storage upload failed or storage configuration is invalid." });
        }

        string? signedUrl;

        try
        {
            signedUrl = await _messagingFileStorage.CreateSignedDownloadUrlAsync(
                objectPath,
                300,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var deleted = await _messagingFileStorage.DeleteObjectAsync(
                objectPath,
                CancellationToken.None);

            if (!deleted)
            {
                _logger.LogWarning(
                    "Cleanup after cancelled messaging upload failed for conversation {ConversationId} and message {MessageId}.",
                    conversationId,
                    messageId);
            }

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Signing messaging attachment failed for conversation {ConversationId} and message {MessageId}.",
                conversationId,
                messageId);

            signedUrl = null;
        }

        if (string.IsNullOrWhiteSpace(signedUrl))
        {
            var deleted = await _messagingFileStorage.DeleteObjectAsync(
                objectPath,
                CancellationToken.None);

            if (!deleted)
            {
                _logger.LogWarning(
                    "Cleanup after messaging attachment signing failure failed for conversation {ConversationId} and message {MessageId}.",
                    conversationId,
                    messageId);
            }

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Unable to create a download URL." });
        }

        messageForUpdate.Attachments.Add(objectPath);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Persisting messaging attachment metadata failed for conversation {ConversationId} and message {MessageId}.",
                conversationId,
                messageId);

            var deleted = await _messagingFileStorage.DeleteObjectAsync(
                objectPath,
                CancellationToken.None);

            if (!deleted)
            {
                _logger.LogWarning(
                    "Cleanup after messaging attachment persistence failure failed for conversation {ConversationId} and message {MessageId}.",
                    conversationId,
                    messageId);
            }

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Failed to persist attachment metadata." });
        }

        return Ok(new { fileUrl = signedUrl });
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