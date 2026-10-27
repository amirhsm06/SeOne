using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api")]
public class SupportController : ApiControllerBase
{
    private const long MaximumFileSizeBytes = 10_000_000;

    private static readonly IReadOnlyDictionary<string, string> AllowedMimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp",
            [".gif"] = "image/gif",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".ppt"] = "application/vnd.ms-powerpoint",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            [".txt"] = "text/plain"
        };

    private readonly SeOneDbContext _db;
    private readonly ISupportFileStorageService _fileStorage;
    private readonly ILogger<SupportController> _logger;

    public SupportController(
        SeOneDbContext db,
        ISupportFileStorageService fileStorage,
        ILogger<SupportController> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    [HttpPost("contact")]
    [AllowAnonymous]
    public async Task<IActionResult> Contact(
        [FromBody] ContactRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new
            {
                message = "Name, email, subject, and message are required."
            });
        }

        Guid? userId = null;

        if (CurrentUserId.HasValue)
            userId = CurrentUserId.Value;

        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = request.Email.Trim(),
            UserName = request.Name?.Trim(),
            Subject = request.Subject?.Trim() ?? "Contact form",
            Category = "general",
            Priority = "medium",
            Status = "open",
            Description = request.Message.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = ticket.Id,
            name = ticket.UserName ?? request.Name,
            email = ticket.UserEmail,
            subject = ticket.Subject,
            message = ticket.Description,
            createdAt = ticket.CreatedAt
        });
    }

    [HttpGet("support/tickets")]
    [Authorize]
    public async Task<IActionResult> Tickets(
        [FromQuery] string? status,
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var isSupport = IsSupportUser();

        var query = _db.SupportTickets
            .AsNoTracking()
            .Include(t => t.User)
            .Include(t => t.AssignedTo)
            .Include(t => t.Messages)
            .AsQueryable();

        if (!isSupport)
            query = query.Where(t => t.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(t => t.Category == category);

        var tickets = await query
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync(cancellationToken);

        var result = new List<object>(tickets.Count);

        foreach (var ticket in tickets)
        {
            result.Add(await MapTicketAsync(
                ticket,
                isSupport,
                cancellationToken));
        }

        return Ok(result);
    }

    [HttpPost("support/tickets")]
    [Authorize]
    public async Task<IActionResult> CreateTicket(
        [FromBody] CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var user = await _db.Users.FindAsync(
            new object[] { userId },
            cancellationToken);

        if (user is null)
            return NotFound();

        var now = DateTime.UtcNow;

        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = user.Email ?? string.Empty,
            UserName = user.FullName,
            Subject = request.Subject.Trim(),
            Category = Normalize(request.Category, "general"),
            Priority = Normalize(request.Priority, "medium"),
            Status = "open",
            Description = request.Description.Trim(),
            Attachments = NormalizeSubmittedAttachments(request.Attachments),
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await MapTicketAsync(
            ticket,
            false,
            cancellationToken));
    }

    [HttpGet("support/tickets/{ticketId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetTicket(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var support = IsSupportUser();

        var ticket = await _db.SupportTickets
            .Include(t => t.User)
            .Include(t => t.AssignedTo)
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(
                t => t.Id == ticketId,
                cancellationToken);

        if (ticket is null)
            return NotFound();

        if (!support && ticket.UserId != userId)
            return Forbid();

        return Ok(await MapTicketAsync(
            ticket,
            support,
            cancellationToken));
    }

    [HttpPatch("support/tickets/{ticketId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateTicket(
        Guid ticketId,
        [FromBody] UpdateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var ticket = await GetOwnedTicket(ticketId, cancellationToken);

        if (ticket is null)
            return NotFound();

        if (!IsSupportUser() && ticket.UserId != RequireUserId())
            return Forbid();

        if (request.Subject is not null)
            ticket.Subject = request.Subject.Trim();

        if (request.Category is not null)
            ticket.Category = Normalize(request.Category, ticket.Category);

        if (request.Priority is not null)
            ticket.Priority = Normalize(request.Priority, ticket.Priority);

        if (request.Status is not null)
        {
            if (!IsSupportUser())
                return Forbid();

            var status = request.Status.Trim().ToLowerInvariant();
            if (status != "open" &&
                status != "in_progress" &&
                status != "resolved" &&
                status != "closed")
            {
                return BadRequest(new { message = "Invalid ticket status." });
            }

            ticket.Status = status;
            ticket.ResolvedAt =
                status == "resolved" || status == "closed"
                    ? DateTime.UtcNow
                    : null;
        }

        ticket.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await MapTicketAsync(
            ticket,
            IsSupportUser(),
            cancellationToken));
    }

    [HttpPost("support/tickets/{ticketId:guid}/close")]
    [Authorize]
    public Task<IActionResult> Close(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        return SetTicketStatus(ticketId, "closed", cancellationToken);
    }

    [HttpPost("support/tickets/{ticketId:guid}/reopen")]
    [Authorize]
    public Task<IActionResult> Reopen(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        return SetTicketStatus(ticketId, "open", cancellationToken);
    }

    [HttpPost("support/tickets/{ticketId:guid}/messages")]
    [Authorize]
    public async Task<IActionResult> AddMessage(
        Guid ticketId,
        [FromBody] CreateSupportMessageRequest request,
        CancellationToken cancellationToken)
    {
        var ticket = await _db.SupportTickets
            .Include(t => t.User)
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(
                t => t.Id == ticketId,
                cancellationToken);

        if (ticket is null)
            return NotFound();

        var userId = RequireUserId();
        var support = IsSupportUser();

        if (!support && ticket.UserId != userId)
            return Forbid();

        var user = await _db.Users.FindAsync(
            new object[] { userId },
            cancellationToken);

        if (user is null)
            return NotFound();

        var message = new SupportMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            UserId = userId,
            UserName = user.FullName,
            IsFromSupport = support,
            Content = request.Content.Trim(),
            Attachments = NormalizeSubmittedAttachments(request.Attachments),
            CreatedAt = DateTime.UtcNow,
            IsInternal = support && request.IsInternal
        };

        _db.SupportMessages.Add(message);

        ticket.UpdatedAt = DateTime.UtcNow;

        if (support && !message.IsInternal)
            ticket.Status = "in_progress";

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await MapMessageAsync(message, cancellationToken));
    }

    [HttpGet("support/tickets/{ticketId:guid}/messages")]
    [Authorize]
    public async Task<IActionResult> Messages(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var ticket = await _db.SupportTickets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Id == ticketId,
                cancellationToken);

        if (ticket is null)
            return NotFound();

        var userId = RequireUserId();
        var support = IsSupportUser();

        if (!support && ticket.UserId != userId)
            return Forbid();

        var query = _db.SupportMessages
            .AsNoTracking()
            .Where(m => m.TicketId == ticketId);

        if (!support)
            query = query.Where(m => !m.IsInternal);

        var messages = await query
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = new List<object>(messages.Count);

        foreach (var message in messages)
        {
            result.Add(await MapMessageAsync(message, cancellationToken));
        }

        return Ok(result);
    }

    [HttpPost("support/tickets/{ticketId:guid}/messages/{messageId:guid}/upload")]
    [Authorize]
    [RequestSizeLimit(10_500_000)]
    public async Task<IActionResult> Upload(
        Guid ticketId,
        Guid messageId,
        IFormFile? attachment,
        CancellationToken cancellationToken)
    {
        var ticket = await _db.SupportTickets
            .FirstOrDefaultAsync(
                t => t.Id == ticketId,
                cancellationToken);

        if (ticket is null)
            return NotFound();

        var userId = RequireUserId();
        var support = IsSupportUser();

        if (!support && ticket.UserId != userId)
            return Forbid();

        var message = await _db.SupportMessages
            .FirstOrDefaultAsync(
                m => m.Id == messageId && m.TicketId == ticketId,
                cancellationToken);

        if (message is null)
            return NotFound();

        if (!support &&
            (message.UserId != userId || message.IsInternal))
        {
            return Forbid();
        }

        if (attachment is null || attachment.Length <= 0)
        {
            return BadRequest(new
            {
                message = "An attachment is required."
            });
        }

        if (attachment.Length > MaximumFileSizeBytes)
        {
            return BadRequest(new
            {
                message = "The attachment exceeds the 10 MB limit."
            });
        }

        var extension = Path.GetExtension(attachment.FileName)
            .ToLowerInvariant();

        if (!AllowedMimeTypes.TryGetValue(extension, out var expectedMimeType))
        {
            return BadRequest(new
            {
                message = "This attachment file type is not supported."
            });
        }

        var contentType = attachment.ContentType?
            .Split(';', 2)[0]
            .Trim();

        if (string.IsNullOrWhiteSpace(contentType) ||
            !string.Equals(
                contentType,
                expectedMimeType,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "The attachment content type does not match its file type."
            });
        }

        var objectPath =
            $"support/{ticketId:D}/{messageId:D}/{Guid.NewGuid():N}{extension}";

        if (!_fileStorage.IsSupportObjectPath(objectPath))
        {
            return BadRequest(new
            {
                message = "The attachment path is invalid."
            });
        }

        await using (var stream = attachment.OpenReadStream())
        {
            var uploaded = await _fileStorage.UploadAsync(
                objectPath,
                stream,
                contentType,
                attachment.Length,
                cancellationToken);

            if (!uploaded)
            {
                return StatusCode(503, new
                {
                    message = "Support attachment storage is unavailable. The upload was not saved."
                });
            }
        }

        // Create the signed URL before committing the database reference.
        var fileUrl = await _fileStorage.CreateSignedDownloadUrlAsync(
            objectPath,
            300,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            await DeleteUploadedObjectQuietlyAsync(objectPath);

            return StatusCode(503, new
            {
                message = "A secure download link could not be created."
            });
        }

        var previousUpdatedAt = ticket.UpdatedAt;

        message.Attachments.Add(objectPath);
        ticket.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            message.Attachments.Remove(objectPath);
            ticket.UpdatedAt = previousUpdatedAt;

            await DeleteUploadedObjectQuietlyAsync(objectPath);

            throw;
        }

        return Ok(new { fileUrl });
    }

    [HttpGet("support/faq")]
    [AllowAnonymous]
    public IActionResult Faq([FromQuery] string? category)
    {
        var faq = new[]
        {
            new
            {
                id = "account-1",
                category = "account",
                question = "How do I change my password?",
                answer = "Open your account settings and use Change Password.",
                order = 1
            },
            new
            {
                id = "booking-1",
                category = "booking",
                question = "How do I book a teacher?",
                answer = "Choose a teacher, select an available slot, and create a booking.",
                order = 2
            },
            new
            {
                id = "payment-1",
                category = "billing",
                question = "How are paid courses purchased?",
                answer = "Paid courses require a payment intent before enrollment is created.",
                order = 3
            },
            new
            {
                id = "learning-1",
                category = "academic",
                question = "How is lesson progress saved?",
                answer = "Lesson progress is saved through the learning/progress endpoints while you study.",
                order = 4
            }
        }.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            faq = faq.Where(x =>
                x.category.Equals(
                    category,
                    StringComparison.OrdinalIgnoreCase));
        }

        return Ok(faq);
    }

    private bool IsSupportUser()
    {
        return User.IsInRole("Admin") || User.IsInRole("Teacher");
    }

    private async Task<SupportTicket?> GetOwnedTicket(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _db.SupportTickets
            .Include(t => t.User)
            .Include(t => t.AssignedTo)
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(
                t => t.Id == id,
                cancellationToken);
    }

    private async Task<IActionResult> SetTicketStatus(
        Guid ticketId,
        string status,
        CancellationToken cancellationToken)
    {
        var ticket = await GetOwnedTicket(ticketId, cancellationToken);

        if (ticket is null)
            return NotFound();

        var userId = RequireUserId();

        if (!IsSupportUser() && ticket.UserId != userId)
            return Forbid();

        ticket.Status = status;
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.ResolvedAt =
            status == "resolved" || status == "closed"
                ? DateTime.UtcNow
                : null;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await MapTicketAsync(
            ticket,
            IsSupportUser(),
            cancellationToken));
    }

    private async Task<object> MapTicketAsync(
        SupportTicket ticket,
        bool support,
        CancellationToken cancellationToken)
    {
        var visibleMessages = ticket.Messages
            .Where(m => support || !m.IsInternal)
            .OrderBy(m => m.CreatedAt)
            .ToList();

        var messages = new List<object>(visibleMessages.Count);

        foreach (var message in visibleMessages)
        {
            messages.Add(await MapMessageAsync(message, cancellationToken));
        }

        return new
        {
            id = ticket.Id,
            userId = ticket.UserId,
            userName = ticket.UserName ?? ticket.User?.FullName,
            userEmail = ticket.UserEmail,
            subject = ticket.Subject,
            category = ticket.Category,
            priority = ticket.Priority,
            status = ticket.Status,
            description = ticket.Description,
            attachments = ticket.Attachments,
            createdAt = ticket.CreatedAt,
            updatedAt = ticket.UpdatedAt,
            resolvedAt = ticket.ResolvedAt,
            assignedTo = ticket.AssignedToId,
            assignedToName = ticket.AssignedTo?.FullName,
            messages
        };
    }

    private async Task<object> MapMessageAsync(
        SupportMessage message,
        CancellationToken cancellationToken)
    {
        var attachmentUrls = new List<string>();
        var expectedPrefix =
            $"support/{message.TicketId:D}/{message.Id:D}/";

        foreach (var attachment in message.Attachments ?? new List<string>())
        {
            if (string.IsNullOrWhiteSpace(attachment))
                continue;

            // Never return private object paths directly to the client.
            if (attachment.StartsWith("support/", StringComparison.Ordinal))
            {
                if (!_fileStorage.IsSupportObjectPath(attachment) ||
                    !attachment.StartsWith(
                        expectedPrefix,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var signedUrl =
                    await _fileStorage.CreateSignedDownloadUrlAsync(
                        attachment,
                        300,
                        cancellationToken);

                if (!string.IsNullOrWhiteSpace(signedUrl))
                    attachmentUrls.Add(signedUrl);

                continue;
            }

            // Preserve legacy URLs and non-Supabase attachment links.
            attachmentUrls.Add(attachment);
        }

        return new
        {
            id = message.Id,
            ticketId = message.TicketId,
            userId = message.UserId,
            userName = message.UserName,
            isFromSupport = message.IsFromSupport,
            content = message.Content,
            attachments = attachmentUrls,
            createdAt = message.CreatedAt,
            isInternal = message.IsInternal
        };
    }

    private List<string> NormalizeSubmittedAttachments(
        IEnumerable<string>? attachments)
    {
        return (attachments ?? Enumerable.Empty<string>())
            .Where(a =>
                !string.IsNullOrWhiteSpace(a) &&
                !_fileStorage.IsSupportObjectPath(a))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task DeleteUploadedObjectQuietlyAsync(string objectPath)
    {
        try
        {
            var deleted = await _fileStorage.DeleteObjectAsync(
                objectPath,
                CancellationToken.None);

            if (!deleted)
            {
                _logger.LogWarning(
                    "Could not clean up support attachment object {ObjectPath}.",
                    objectPath);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Error cleaning up support attachment object {ObjectPath}.",
                objectPath);
        }
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().ToLowerInvariant();
    }
}

public sealed class ContactRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class CreateTicketRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
    public string? Priority { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string>? Attachments { get; set; }
}

public sealed class UpdateTicketRequest
{
    public string? Subject { get; set; }
    public string? Category { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }
}

public sealed class CreateSupportMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public List<string>? Attachments { get; set; }
    public bool IsInternal { get; set; }
}