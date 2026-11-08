using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/class-groups")]
[Authorize]
public sealed class ClassGroupsController : ControllerBase
{
    private readonly SeOneDbContext _db;

    public ClassGroupsController(SeOneDbContext db) => _db = db;

    [HttpGet("{groupId:guid}")]
    public async Task<IActionResult> GetGroup(Guid groupId, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        if (!await IsActiveMemberAsync(groupId, userId, cancellationToken)) return Forbid();

        var group = await _db.ClassGroups.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == groupId, cancellationToken);
        if (group is null || !group.IsActive) return NotFound();

        var courseClass = await _db.CourseClasses.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == group.ClassId, cancellationToken);

        string teacherName = "Unassigned teacher";
        if (courseClass is not null)
        {
            // CourseClass.TeacherId is the assigned Identity User.Id.
            var teacherUser = await _db.Users.AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == courseClass.TeacherId,
                    cancellationToken);
            teacherName = GetDisplayName(teacherUser);
        }

        var memberCount = await _db.ClassGroupMembers.AsNoTracking()
            .CountAsync(x => x.GroupId == groupId && x.IsActive, cancellationToken);

        return Ok(new ClassGroupDto
        {
            Id = group.Id,
            ClassId = group.ClassId,
            Title = group.Title,
            GroupType = group.GroupType,
            TeacherName = teacherName,
            MemberCount = memberCount,
            IsActive = group.IsActive
        });
    }

    [HttpGet("{groupId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid groupId,
        [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        if (!await IsActiveMemberAsync(groupId, userId, cancellationToken)) return Forbid();

        limit = Math.Clamp(limit, 1, 200);
        var messages = await _db.ClassGroupMessages.AsNoTracking()
            .Where(x => x.GroupId == groupId)
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

        var items = messages.Select(x => new ClassGroupMessageDto
        {
            Id = x.Id,
            GroupId = x.GroupId,
            SenderId = x.SenderId,
            SenderName = userNames.GetValueOrDefault(x.SenderId, "Unknown user"),
            SenderRole = x.SenderRole,
            Content = x.Content,
            CreatedAt = DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc)
        }).ToList();

        return Ok(new { items });
    }

    [HttpPost("{groupId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid groupId,
        [FromBody] CreateClassGroupMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var membership = await _db.ClassGroupMembers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.GroupId == groupId && x.UserId == userId && x.IsActive, cancellationToken);
        if (membership is null) return Forbid();

        var groupIsActive = await _db.ClassGroups.AsNoTracking()
            .AnyAsync(x => x.Id == groupId && x.IsActive, cancellationToken);
        if (!groupIsActive) return NotFound();

        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return BadRequest(new ProblemDetails { Title = "Message content is required.", Status = 400 });
        if (content.Length > 5000)
            return BadRequest(new ProblemDetails { Title = "Message content cannot exceed 5000 characters.", Status = 400 });

        var entity = new ClassGroupMessage
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SenderId = userId,
            SenderRole = membership.Role,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
        _db.ClassGroupMessages.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var sender = await _db.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        return CreatedAtAction(nameof(GetMessages), new { groupId, limit = 200 }, new ClassGroupMessageDto
        {
            Id = entity.Id,
            GroupId = entity.GroupId,
            SenderId = entity.SenderId,
            SenderName = GetDisplayName(sender),
            SenderRole = entity.SenderRole,
            Content = entity.Content,
            CreatedAt = entity.CreatedAt
        });
    }

    private async Task<bool> IsActiveMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken)
    {
        var groupActive = await _db.ClassGroups.AsNoTracking()
            .AnyAsync(x => x.Id == groupId && x.IsActive, cancellationToken);
        if (!groupActive) return false;
        return await _db.ClassGroupMembers.AsNoTracking()
            .AnyAsync(x => x.GroupId == groupId && x.UserId == userId && x.IsActive, cancellationToken);
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
