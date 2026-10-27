using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeOne.Api.DTOs;
using SeOne.Domain.Entities;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api.Controllers;

[ApiController]
[Route("api/admin/announcements")]
[Authorize(Roles = "Admin")]
public sealed class AdminAnnouncementsController : ControllerBase
{
    private readonly SeOneDbContext _db;

    public AdminAnnouncementsController(SeOneDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<AnnouncementResponse>>> List(
        [FromQuery] string status = "all",
        CancellationToken cancellationToken = default)
    {
        var filter = (status ?? "all").Trim().ToLowerInvariant();
        if (filter is not ("all" or "draft" or "scheduled" or "published"))
        {
            return BadRequest(new { message = "Invalid status filter." });
        }

        var now = DateTime.UtcNow;
        var query = _db.Announcements.AsNoTracking().AsQueryable();

        if (filter == "draft")
        {
            query = query.Where(x => x.Status == "draft");
        }
        else if (filter == "scheduled")
        {
            query = query.Where(x =>
                x.Status == "scheduled" &&
                x.PublishAt.HasValue &&
                x.PublishAt.Value > now);
        }
        else if (filter == "published")
        {
            query = query.Where(x =>
                x.Status == "published" ||
                (x.Status == "scheduled" &&
                 x.PublishAt.HasValue &&
                 x.PublishAt.Value <= now));
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(x => AnnouncementResponse.From(x, now)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<AnnouncementResponse>> Create(
        [FromBody] CreateAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var item = new Announcement
        {
            Id = Guid.NewGuid(),
            Title = request.Title ?? string.Empty,
            Message = request.Message ?? string.Empty,
            Audience = request.Audience ?? string.Empty,
            Kind = request.Kind ?? string.Empty,
            Status = request.Status ?? string.Empty,
            ActionUrl = request.ActionUrl,
            ActionLabel = request.ActionLabel,
            PublishAt = request.PublishAt?.UtcDateTime,
            ExpiresAt = request.ExpiresAt?.UtcDateTime,
            CreatedAt = now,
            UpdatedAt = now
        };

        var validationError = AnnouncementContract.ValidateAndNormalize(item, now);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        _db.Announcements.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(AnnouncementResponse.From(item, now));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<AnnouncementResponse>> Update(
        Guid id,
        [FromBody] JsonElement patch,
        CancellationToken cancellationToken)
    {
        if (patch.ValueKind != JsonValueKind.Object)
        {
            return BadRequest(new { message = "The patch body must be a JSON object." });
        }

        var item = await _db.Announcements
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "title", "message", "audience", "kind", "status",
            "actionUrl", "actionLabel", "publishAt", "expiresAt"
        };

        var statusWasProvided = false;
        foreach (var property in patch.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                return BadRequest(new { message = $"Unsupported patch field: {property.Name}." });
            }

            var name = property.Name.ToLowerInvariant();
            if (name is "title" or "message" or "audience" or "kind" or "status")
            {
                if (!TryReadString(property.Value, allowNull: false, out var text))
                {
                    return BadRequest(new { message = $"{property.Name} must be a string." });
                }

                switch (name)
                {
                    case "title": item.Title = text ?? string.Empty; break;
                    case "message": item.Message = text ?? string.Empty; break;
                    case "audience": item.Audience = text ?? string.Empty; break;
                    case "kind": item.Kind = text ?? string.Empty; break;
                    case "status":
                        item.Status = text ?? string.Empty;
                        statusWasProvided = true;
                        break;
                }
            }
            else if (name is "actionurl" or "actionlabel")
            {
                if (!TryReadString(property.Value, allowNull: true, out var text))
                {
                    return BadRequest(new { message = $"{property.Name} must be a string or null." });
                }

                if (name == "actionurl") item.ActionUrl = text;
                else item.ActionLabel = text;
            }
            else if (name is "publishat" or "expiresat")
            {
                if (!TryReadDateTime(property.Value, out var date))
                {
                    return BadRequest(new { message = $"{property.Name} must be an ISO 8601 date or null." });
                }

                if (name == "publishat") item.PublishAt = date;
                else item.ExpiresAt = date;
            }
        }

        var now = DateTime.UtcNow;
        // Promote elapsed schedules when they are next edited. A request that explicitly
        // sets status=scheduled is still required to provide a future publishAt.
        if (!statusWasProvided && item.Status == "scheduled" &&
            item.PublishAt.HasValue && item.PublishAt.Value <= now)
        {
            item.Status = "published";
        }

        var validationError = AnnouncementContract.ValidateAndNormalize(item, now);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        item.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(AnnouncementResponse.From(item, now));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await _db.Announcements
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        _db.Announcements.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static bool TryReadString(
        JsonElement value,
        bool allowNull,
        out string? result)
    {
        if (value.ValueKind == JsonValueKind.Null && allowNull)
        {
            result = null;
            return true;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            result = null;
            return false;
        }

        result = value.GetString();
        return result is not null;
    }

    private static bool TryReadDateTime(JsonElement value, out DateTime? result)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            result = null;
            return true;
        }

        if (value.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            result = parsed.UtcDateTime;
            return true;
        }

        result = null;
        return false;
    }
}
