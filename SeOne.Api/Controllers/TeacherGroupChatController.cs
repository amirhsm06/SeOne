using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/teacher-group-chat")]
[Authorize]
public sealed class TeacherGroupChatController : ControllerBase
{
    private readonly SeOneDbContext _db;

    public TeacherGroupChatController(SeOneDbContext db) => _db = db;

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages([FromQuery] int limit = 200, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        if (!await CanUseTeacherGroupChatAsync(userId, cancellationToken)) return Forbid();

        limit = Math.Clamp(limit, 1, 200);
        var messages = await _db.TeacherGroupChatMessages
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
        messages.Reverse();

        var senderIds = messages.Select(x => x.SenderId).Distinct().ToArray();
        var users = await _db.Users.AsNoTracking()
            .Where(x => senderIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var userNames = users.ToDictionary(x => x.Id, GetDisplayName);

        var items = messages.Select(x => new GroupChatMessageDto
        {
            Id = x.Id,
            SenderId = x.SenderId,
            SenderName = userNames.GetValueOrDefault(x.SenderId, "Unknown user"),
            SenderRole = x.SenderRole,
            Content = x.Content,
            CreatedAt = DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc),
            IsAnnouncement = x.IsAnnouncement
        }).ToList();

        return Ok(new { items });
    }

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage(
        [FromBody] CreateGroupMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        if (!await CanUseTeacherGroupChatAsync(userId, cancellationToken)) return Forbid();

        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return BadRequest(new ProblemDetails { Title = "Message content is required.", Status = 400 });
        if (content.Length > 5000)
            return BadRequest(new ProblemDetails { Title = "Message content cannot exceed 5000 characters.", Status = 400 });

        var isAdmin = User.IsInRole("Admin");
        if (request.IsAnnouncement && !isAdmin)
            return Forbid();

        var entity = new TeacherGroupChatMessage
        {
            Id = Guid.NewGuid(),
            SenderId = userId,
            SenderRole = isAdmin ? "Admin" : "Teacher",
            Content = content,
            IsAnnouncement = request.IsAnnouncement,
            CreatedAt = DateTime.UtcNow
        };

        _db.TeacherGroupChatMessages.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var sender = await _db.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        var result = new GroupChatMessageDto
        {
            Id = entity.Id,
            SenderId = entity.SenderId,
            SenderName = GetDisplayName(sender),
            SenderRole = entity.SenderRole,
            Content = entity.Content,
            CreatedAt = entity.CreatedAt,
            IsAnnouncement = entity.IsAnnouncement
        };

        return CreatedAtAction(nameof(GetMessages), new { limit = 200 }, result);
    }

    private async Task<bool> CanUseTeacherGroupChatAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin")) return true;
        if (!User.IsInRole("Teacher")) return false;

        return await _db.Users.AsNoTracking().AnyAsync(
            x => x.Id == userId
                 && x.Role == UserRole.Teacher
                 && x.AccountStatus == "active",
            cancellationToken);
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static string GetDisplayName(User? user)
    {
        if (user is null) return "Unknown user";
        if (!string.IsNullOrWhiteSpace(user.FullName)) return user.FullName;
        var name = string.Join(" ", new[] { user.FirstName, user.FamilyName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(name)
            ? user.UserName ?? user.Email ?? "Unknown user"
            : name;
    }
}
