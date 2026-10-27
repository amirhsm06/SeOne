using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Api.DTOs;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/announcements")]
[Authorize]
public sealed class AnnouncementsController : ControllerBase
{
    private readonly SeOneDbContext _db;

    public AnnouncementsController(SeOneDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<AnnouncementResponse>>> GetDashboardAnnouncements(
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var audience = GetCurrentAudience();
        if (audience is null)
        {
            return Forbid();
        }

        var now = DateTime.UtcNow;
        var safeLimit = Math.Clamp(limit, 1, 50);

        var items = await _db.Announcements
            .AsNoTracking()
            .Where(x =>
                (x.Status == "published" || x.Status == "scheduled") &&
                (x.PublishAt == null || x.PublishAt <= now) &&
                (x.ExpiresAt == null || x.ExpiresAt > now) &&
                (x.Audience == "all" || x.Audience == audience))
            .OrderByDescending(x => x.CreatedAt)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);

        var ids = items.Select(x => x.Id).ToList();
        var states = ids.Count == 0
            ? new Dictionary<Guid, AnnouncementUserState>()
            : await _db.AnnouncementUserStates
                .AsNoTracking()
                .Where(x => x.UserId == userId && ids.Contains(x.AnnouncementId))
                .ToDictionaryAsync(x => x.AnnouncementId, cancellationToken);

        var response = items.Select(item =>
        {
            states.TryGetValue(item.Id, out var state);
            return AnnouncementResponse.From(
                item,
                now,
                state?.IsRead ?? false,
                state?.IsDismissed ?? false);
        }).ToList();

        return Ok(response);
    }

    [HttpPost("{id:guid}/read")]
    public Task<IActionResult> MarkRead(
        Guid id,
        CancellationToken cancellationToken) => SetState(id, markRead: true, cancellationToken: cancellationToken);

    [HttpPost("{id:guid}/dismiss")]
    public Task<IActionResult> Dismiss(
        Guid id,
        CancellationToken cancellationToken) => SetState(id, markRead: false, cancellationToken: cancellationToken);

    private async Task<IActionResult> SetState(
        Guid announcementId,
        bool markRead,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var audience = GetCurrentAudience();
        if (audience is null)
        {
            return Forbid();
        }

        var now = DateTime.UtcNow;
        var isVisibleToUser = await _db.Announcements
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == announcementId &&
                (x.Status == "published" || x.Status == "scheduled") &&
                (x.PublishAt == null || x.PublishAt <= now) &&
                (x.ExpiresAt == null || x.ExpiresAt > now) &&
                (x.Audience == "all" || x.Audience == audience),
                cancellationToken);

        if (!isVisibleToUser)
        {
            // Do not reveal whether an untargeted or unpublished announcement exists.
            return NotFound();
        }

        var state = await _db.AnnouncementUserStates
            .FirstOrDefaultAsync(
                x => x.AnnouncementId == announcementId && x.UserId == userId,
                cancellationToken);
        var isNew = state is null;
        state ??= new AnnouncementUserState
        {
            AnnouncementId = announcementId,
            UserId = userId
        };

        ApplyState(state, markRead, now);
        if (isNew)
        {
            _db.AnnouncementUserStates.Add(state);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (isNew)
        {
            // Concurrent duplicate first writes are resolved against the composite key;
            // repeated read/dismiss calls therefore remain idempotent.
            _db.Entry(state).State = EntityState.Detached;
            var existing = await _db.AnnouncementUserStates
                .FirstOrDefaultAsync(
                    x => x.AnnouncementId == announcementId && x.UserId == userId,
                    cancellationToken);

            if (existing is null)
            {
                throw;
            }

            ApplyState(existing, markRead, now);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private static void ApplyState(
        AnnouncementUserState state,
        bool markRead,
        DateTime now)
    {
        if (markRead)
        {
            state.IsRead = true;
            state.ReadAt ??= now;
        }
        else
        {
            state.IsDismissed = true;
            state.DismissedAt ??= now;
        }
    }

    private string? GetCurrentAudience()
    {
        if (User.IsInRole("Student")) return "students";
        if (User.IsInRole("Teacher")) return "teachers";
        return null;
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }
}
