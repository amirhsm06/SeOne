using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/teacher-group-chat")]
[Authorize(Roles = "Teacher,Admin")]
public sealed class TeacherGroupChatController : ApiControllerBase
{
    private const int MaximumMessageLength = 5000;
    private readonly SeOneDbContext _db;

    public TeacherGroupChatController(SeOneDbContext db) => _db = db;

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages(
        [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 200)
            return BadRequest(new { message = "limit must be between 1 and 200." });

        var messages = await _db.TeacherGroupChatMessages.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var senderIds = messages.Select(x => x.SenderId).Distinct().ToArray();
        var senders = await _db.Users.AsNoTracking()
            .Where(x => senderIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return Ok(messages
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(message => new
            {
                id = message.Id,
                senderId = message.SenderId,
                senderRole = message.SenderRole,
                senderName = senders.TryGetValue(message.SenderId, out var sender) ? sender.FullName : "Former user",
                senderAvatarUrl = senders.TryGetValue(message.SenderId, out var senderForAvatar) ? senderForAvatar.AvatarUrl : null,
                content = message.Content,
                isAnnouncement = message.IsAnnouncement,
                createdAt = message.CreatedAt
            })
            .ToArray());
    }

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage(
        [FromBody] CreateTeacherGroupChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length > MaximumMessageLength)
            return BadRequest(new { message = $"Message content is required and cannot exceed {MaximumMessageLength} characters." });

        if (request.IsAnnouncement && !User.IsInRole("Admin"))
            return Forbid();

        var senderId = RequireUserId();
        var message = new TeacherGroupChatMessage
        {
            Id = Guid.NewGuid(),
            SenderId = senderId,
            SenderRole = User.IsInRole("Admin") ? "admin" : "teacher",
            Content = content,
            IsAnnouncement = request.IsAnnouncement,
            CreatedAt = DateTime.UtcNow
        };

        _db.TeacherGroupChatMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        var senderName = await _db.Users.AsNoTracking()
            .Where(x => x.Id == senderId)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "User";

        return Ok(new
        {
            id = message.Id,
            senderId = message.SenderId,
            senderRole = message.SenderRole,
            senderName,
            content = message.Content,
            isAnnouncement = message.IsAnnouncement,
            createdAt = message.CreatedAt
        });
    }
}

public sealed class CreateTeacherGroupChatMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public bool IsAnnouncement { get; set; }
}
