using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/class-groups")]
[Authorize]
public sealed class ClassGroupsController : ApiControllerBase
{
    private readonly SeOneDbContext _db;
    private readonly IClassGroupProvisioningService _groupProvisioning;

    public ClassGroupsController(
        SeOneDbContext db,
        IClassGroupProvisioningService groupProvisioning)
    {
        _db = db;
        _groupProvisioning = groupProvisioning;
    }

    [HttpGet("{groupId:guid}")]
    public async Task<IActionResult> GetGroup(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await _db.Set<ClassGroup>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == groupId, cancellationToken);
        if (group is null || !group.IsActive)
            return NotFound(new { message = "Class group not found." });

        // Backfill memberships for older confirmed enrollments and assignments.
        await _groupProvisioning.EnsureForClassAsync(group.ClassId, cancellationToken);

        if (!await CanAccessGroupAsync(groupId, cancellationToken))
            return Forbid();

        var teacherName = await (
            from member in _db.Set<ClassGroupMember>().AsNoTracking()
            join user in _db.Users.AsNoTracking() on member.UserId equals user.Id
            where member.GroupId == groupId && member.IsActive && member.Role == "teacher"
            select user.FullName
        ).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var memberCount = await _db.Set<ClassGroupMember>().AsNoTracking()
            .CountAsync(x => x.GroupId == groupId && x.IsActive, cancellationToken);

        return Ok(new
        {
            id = group.Id,
            title = group.Title,
            teacherName,
            memberCount
        });
    }

    [HttpGet("{groupId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid groupId,
        [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var group = await _db.Set<ClassGroup>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == groupId && x.IsActive, cancellationToken);
        if (group is null)
            return NotFound(new { message = "Class group not found." });

        await _groupProvisioning.EnsureForClassAsync(group.ClassId, cancellationToken);
        if (!await CanAccessGroupAsync(groupId, cancellationToken))
            return Forbid();

        limit = Math.Clamp(limit, 1, 200);
        var messages = await (
            from message in _db.Set<ClassGroupMessage>().AsNoTracking()
            join sender in _db.Users.AsNoTracking() on message.SenderId equals sender.Id
            where message.GroupId == groupId
            orderby message.CreatedAt descending, message.Id descending
            select new
            {
                id = message.Id,
                senderId = message.SenderId,
                senderName = sender.FullName,
                senderRole = message.SenderRole,
                content = message.Content,
                createdAt = message.CreatedAt
            }
        ).Take(limit).ToListAsync(cancellationToken);

        messages.Reverse();
        return Ok(new { items = messages });
    }

    [HttpPost("{groupId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid groupId,
        [FromBody] SendClassGroupMessageRequest? request,
        CancellationToken cancellationToken)
    {
        var group = await _db.Set<ClassGroup>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == groupId && x.IsActive, cancellationToken);
        if (group is null)
            return NotFound(new { message = "Class group not found." });

        await _groupProvisioning.EnsureForClassAsync(group.ClassId, cancellationToken);
        if (!await CanAccessGroupAsync(groupId, cancellationToken))
            return Forbid();

        var content = request?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return BadRequest(new { message = "Message content is required." });
        if (content.Length > 5000)
            return BadRequest(new { message = "Message content cannot exceed 5000 characters." });

        var senderId = RequireUserId();
        var sender = await _db.Users.AsNoTracking()
            .Where(x => x.Id == senderId)
            .Select(x => new { x.Id, x.FullName, x.Role })
            .FirstOrDefaultAsync(cancellationToken);
        if (sender is null)
            return Unauthorized();

        var senderRole = sender.Role switch
        {
            SeOne.Domain.Enums.UserRole.Admin => "admin",
            SeOne.Domain.Enums.UserRole.Teacher => "teacher",
            _ => "student"
        };

        var message = new ClassGroupMessage
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SenderId = senderId,
            SenderRole = senderRole,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        _db.Set<ClassGroupMessage>().Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            id = message.Id,
            senderId = message.SenderId,
            senderName = sender.FullName,
            senderRole = message.SenderRole,
            content = message.Content,
            createdAt = message.CreatedAt
        });
    }

    private async Task<bool> CanAccessGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (!userId.HasValue)
            return false;

        var account = await _db.Users.AsNoTracking()
            .Where(x => x.Id == userId.Value)
            .Select(x => new { x.Role, x.AccountStatus })
            .FirstOrDefaultAsync(cancellationToken);
        if (account is null || account.AccountStatus != "active")
            return false;

        if (account.Role == SeOne.Domain.Enums.UserRole.Admin)
            return true;

        var expectedMemberRole = account.Role switch
        {
            SeOne.Domain.Enums.UserRole.Teacher => "teacher",
            SeOne.Domain.Enums.UserRole.Student => "student",
            _ => string.Empty
        };
        if (expectedMemberRole.Length == 0)
            return false;

        return await _db.Set<ClassGroupMember>().AsNoTracking()
            .AnyAsync(x => x.GroupId == groupId &&
                           x.UserId == userId.Value &&
                           x.Role == expectedMemberRole &&
                           x.IsActive,
                cancellationToken);
    }
}

public sealed class SendClassGroupMessageRequest
{
    public string? Content { get; set; }
}
