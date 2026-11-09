using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/class-groups")]
[Authorize(Roles = "Student,Teacher,Admin")]
public sealed class ClassGroupsController : ApiControllerBase
{
    private const int MaximumMessageLength = 5000;
    private readonly SeOneDbContext _db;

    public ClassGroupsController(SeOneDbContext db) => _db = db;

    [HttpGet("{groupId:guid}")]
    public async Task<IActionResult> GetGroup(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await _db.ClassGroups.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == groupId, cancellationToken);
        if (group is null)
            return NotFound();

        if (!await CanAccessGroupAsync(groupId, cancellationToken))
            return Forbid();

        var classItem = await _db.CourseClasses.AsNoTracking()
            .Include(x => x.Course)
            .Include(x => x.Teacher)
            .FirstOrDefaultAsync(x => x.Id == group.ClassId, cancellationToken);
        if (classItem is null)
            return NotFound();

        var members = await (
            from member in _db.ClassGroupMembers.AsNoTracking()
            join user in _db.Users.AsNoTracking() on member.UserId equals user.Id
            where member.GroupId == groupId && member.IsActive
            orderby member.Role, user.FullName
            select new
            {
                userId = user.Id,
                name = user.FullName,
                avatarUrl = user.AvatarUrl,
                role = member.Role,
                joinedAt = member.JoinedAt
            })
            .ToListAsync(cancellationToken);

        var assignedTeacher = await _db.ClassSlotTeacherAssignments.AsNoTracking()
            .Where(x => x.ClassSlotId == group.ClassId && x.Status == "confirmed")
            .OrderBy(x => x.CreatedAt)
            .Select(x => new { id = x.Teacher.Id, name = x.Teacher.FullName, avatarUrl = x.Teacher.AvatarUrl })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            id = group.Id,
            classId = group.ClassId,
            title = group.Title,
            groupType = group.GroupType,
            isActive = group.IsActive,
            createdAt = group.CreatedAt,
            course = new { id = classItem.CourseId, title = classItem.Course.Title },
            scheduledDate = classItem.ScheduledDate,
            startTime = classItem.StartTime.ToString(@"hh\:mm"),
            endTime = classItem.EndTime.ToString(@"hh\:mm"),
            timeZone = classItem.TimeZone,
            teacher = assignedTeacher,
            members
        });
    }

    [HttpGet("{groupId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid groupId,
        [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 200)
            return BadRequest(new { message = "limit must be between 1 and 200." });

        var group = await _db.ClassGroups.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == groupId, cancellationToken);
        if (group is null)
            return NotFound();
        if (!await CanAccessGroupAsync(groupId, cancellationToken))
            return Forbid();

        var messages = await _db.ClassGroupMessages.AsNoTracking()
            .Where(x => x.GroupId == groupId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var senderIds = messages.Select(x => x.SenderId).Distinct().ToArray();
        var senders = await _db.Users.AsNoTracking()
            .Where(x => senderIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var result = messages
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(message => new
            {
                id = message.Id,
                groupId = message.GroupId,
                senderId = message.SenderId,
                senderRole = message.SenderRole,
                senderName = senders.TryGetValue(message.SenderId, out var sender) ? sender.FullName : "Former user",
                senderAvatarUrl = senders.TryGetValue(message.SenderId, out var senderForAvatar) ? senderForAvatar.AvatarUrl : null,
                content = message.Content,
                createdAt = message.CreatedAt
            })
            .ToArray();

        return Ok(result);
    }

    [HttpPost("{groupId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid groupId,
        [FromBody] CreateClassGroupMessageRequest request,
        CancellationToken cancellationToken)
    {
        var group = await _db.ClassGroups
            .FirstOrDefaultAsync(x => x.Id == groupId, cancellationToken);
        if (group is null)
            return NotFound();
        if (!await CanAccessGroupAsync(groupId, cancellationToken))
            return Forbid();
        if (!group.IsActive)
            return Conflict(new { message = "This class group is no longer active." });

        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length > MaximumMessageLength)
            return BadRequest(new { message = $"Message content is required and cannot exceed {MaximumMessageLength} characters." });

        var senderId = RequireUserId();
        var senderRole = User.IsInRole("Admin") ? "admin"
            : User.IsInRole("Teacher") ? "teacher"
            : "student";

        var message = new ClassGroupMessage
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SenderId = senderId,
            SenderRole = senderRole,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        _db.ClassGroupMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        var senderName = await _db.Users.AsNoTracking()
            .Where(x => x.Id == senderId)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "User";

        return Ok(new
        {
            id = message.Id,
            groupId = message.GroupId,
            senderId = message.SenderId,
            senderRole = message.SenderRole,
            senderName,
            content = message.Content,
            createdAt = message.CreatedAt
        });
    }

    private async Task<bool> CanAccessGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Admin"))
            return true;

        var userId = RequireUserId();

        var isActiveMember = await _db.ClassGroupMembers.AsNoTracking().AnyAsync(
            x => x.GroupId == groupId && x.UserId == userId && x.IsActive,
            cancellationToken);
        if (isActiveMember)
            return true;

        // Existing classes may predate the group-membership provisioning service.
        // Verify the underlying enrollment/teacher assignment as a safe fallback.
        var classId = await _db.ClassGroups.AsNoTracking()
            .Where(x => x.Id == groupId)
            .Select(x => (Guid?)x.ClassId)
            .FirstOrDefaultAsync(cancellationToken);
        if (classId is null)
            return false;

        if (User.IsInRole("Student"))
        {
            return await _db.ClassEnrollments.AsNoTracking().AnyAsync(
                x => x.ClassId == classId.Value && x.StudentId == userId && x.Status == "confirmed",
                cancellationToken);
        }

        if (User.IsInRole("Teacher"))
        {
            return await _db.CourseClasses.AsNoTracking().AnyAsync(
                    x => x.Id == classId.Value && x.TeacherId == userId,
                    cancellationToken)
                || await _db.ClassSlotTeacherAssignments.AsNoTracking().AnyAsync(
                    x => x.ClassSlotId == classId.Value && x.TeacherId == userId && x.Status == "confirmed",
                    cancellationToken);
        }

        return false;
    }
}

public sealed class CreateClassGroupMessageRequest
{
    public string Content { get; set; } = string.Empty;
}
