using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly SeOneDbContext _context;

    public NotificationService(
        SeOneDbContext context)
    {
        _context = context;
    }

    public async Task<List<NotificationDto>> GetAsync(
        Guid userId,
        string? type,
        bool unreadOnly,
        int? limit,
        int? offset)
    {
        var query = _context.Set<Notification>()
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                (x.ExpiresAt == null ||
                 x.ExpiresAt > DateTime.UtcNow));

        if (!string.IsNullOrWhiteSpace(type) &&
            Enum.TryParse<NotificationType>(
                type,
                true,
                out var notificationType))
        {
            query = query.Where(
                x => x.Type == notificationType);
        }

        if (unreadOnly)
        {
            query = query.Where(x => !x.IsRead);
        }

        query = query
            .OrderByDescending(x => x.CreatedAt);

        if (offset.HasValue &&
            offset.Value > 0)
        {
            query = query.Skip(offset.Value);
        }

        if (limit.HasValue)
        {
            var safeLimit =
                Math.Clamp(limit.Value, 1, 100);

            query = query.Take(safeLimit);
        }
        else
        {
            query = query.Take(50);
        }

        var notifications =
            await query.ToListAsync();

        return notifications
            .Select(MapNotification)
            .ToList();
    }

    public async Task<NotificationStatsDto> GetStatsAsync(
        Guid userId)
    {
        var notifications =
            await _context.Set<Notification>()
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId &&
                    (x.ExpiresAt == null ||
                     x.ExpiresAt > DateTime.UtcNow))
                .Select(x => new
                {
                    x.Type,
                    x.IsRead
                })
                .ToListAsync();

        return new NotificationStatsDto
        {
            Total = notifications.Count,
            Unread = notifications.Count(
                x => !x.IsRead),

            ByType = new NotificationTypeStatsDto
            {
                Booking = notifications.Count(
                    x => x.Type == NotificationType.Booking),

                Payment = notifications.Count(
                    x => x.Type == NotificationType.Payment),

                Assignment = notifications.Count(
                    x => x.Type == NotificationType.Assignment),

                Message = notifications.Count(
                    x => x.Type == NotificationType.Message),

                Promotion = notifications.Count(
                    x => x.Type == NotificationType.Promotion),

                System = notifications.Count(
                    x => x.Type == NotificationType.System)
            }
        };
    }

    public async Task<NotificationDto?> GetByIdAsync(
        Guid userId,
        Guid notificationId)
    {
        var notification =
            await _context.Set<Notification>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == notificationId &&
                    x.UserId == userId);

        return notification is null
            ? null
            : MapNotification(notification);
    }

    public async Task<bool> MarkAsReadAsync(
        Guid userId,
        Guid notificationId)
    {
        var notification =
            await _context.Set<Notification>()
                .FirstOrDefaultAsync(x =>
                    x.Id == notificationId &&
                    x.UserId == userId);

        if (notification is null)
        {
            return false;
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        return true;
    }

    public async Task MarkAllAsReadAsync(
        Guid userId)
    {
        var notifications =
            await _context.Set<Notification>()
                .Where(x =>
                    x.UserId == userId &&
                    !x.IsRead)
                .ToListAsync();

        if (notifications.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(
        Guid userId,
        Guid notificationId)
    {
        var notification =
            await _context.Set<Notification>()
                .FirstOrDefaultAsync(x =>
                    x.Id == notificationId &&
                    x.UserId == userId);

        if (notification is null)
        {
            return false;
        }

        _context.Set<Notification>()
            .Remove(notification);

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task ClearAsync(
        Guid userId,
        bool readOnly,
        DateTime? olderThan)
    {
        var query =
            _context.Set<Notification>()
                .Where(x => x.UserId == userId);

        if (readOnly)
        {
            query = query.Where(
                x => x.IsRead);
        }

        if (olderThan.HasValue)
        {
            var date = olderThan.Value.ToUniversalTime();

            query = query.Where(
                x => x.CreatedAt < date);
        }

        var notifications =
            await query.ToListAsync();

        if (notifications.Count == 0)
        {
            return;
        }

        _context.Set<Notification>()
            .RemoveRange(notifications);

        await _context.SaveChangesAsync();
    }

    public async Task<NotificationActionResultDto?>
        ExecuteActionAsync(
            Guid userId,
            Guid notificationId,
            Dictionary<string, object?>? actionData)
    {
        var notification =
            await _context.Set<Notification>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == notificationId &&
                    x.UserId == userId);

        if (notification is null)
        {
            return null;
        }

        if (!notification.IsRead)
        {
            var entity =
                await _context.Set<Notification>()
                    .FirstAsync(x =>
                        x.Id == notificationId &&
                        x.UserId == userId);

            entity.IsRead = true;
            entity.ReadAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        return new NotificationActionResultDto
        {
            Success = true,
            RedirectUrl = notification.ActionUrl
        };
    }

    public async Task<NotificationDto?> CreateTestAsync(
        Guid userId,
        string type)
    {
        if (!TryParseType(
                type,
                out var notificationType))
        {
            return null;
        }

        var titles = new Dictionary<
            NotificationType,
            string>
        {
            [NotificationType.Booking] =
                "Booking notification",

            [NotificationType.Payment] =
                "Payment notification",

            [NotificationType.Assignment] =
                "Assignment notification",

            [NotificationType.Message] =
                "Message notification",

            [NotificationType.Promotion] =
                "Promotion notification",

            [NotificationType.System] =
                "System notification"
        };

        var messages = new Dictionary<
            NotificationType,
            string>
        {
            [NotificationType.Booking] =
                "You have a new booking update.",

            [NotificationType.Payment] =
                "Your payment status has been updated.",

            [NotificationType.Assignment] =
                "You have a new assignment update.",

            [NotificationType.Message] =
                "You have a new message.",

            [NotificationType.Promotion] =
                "There is a new promotion available.",

            [NotificationType.System] =
                "This is a test system notification."
        };

        return await CreateAsync(
            userId,
            notificationType.ToString(),
            titles[notificationType],
            messages[notificationType]);
    }

    public async Task<NotificationDto> CreateAsync(
        Guid userId,
        string type,
        string title,
        string message,
        Dictionary<string, object?>? data = null,
        string? actionUrl = null,
        string? actionLabel = null,
        DateTime? expiresAt = null)
    {
        if (!TryParseType(
                type,
                out var notificationType))
        {
            throw new ArgumentException(
                "Invalid notification type.",
                nameof(type));
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = notificationType,
            Title = title.Trim(),
            Message = message.Trim(),
            DataJson = data is null
                ? null
                : JsonSerializer.Serialize(data),
            IsRead = false,
            ReadAt = null,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt?.ToUniversalTime(),
            ActionUrl = string.IsNullOrWhiteSpace(actionUrl)
                ? null
                : actionUrl.Trim(),
            ActionLabel = string.IsNullOrWhiteSpace(actionLabel)
                ? null
                : actionLabel.Trim()
        };

        _context.Set<Notification>()
            .Add(notification);

        await _context.SaveChangesAsync();

        return MapNotification(notification);
    }

    private static bool TryParseType(
        string? type,
        out NotificationType result)
    {
        return Enum.TryParse(
            type,
            true,
            out result);
    }

    private static NotificationDto MapNotification(
        Notification notification)
    {
        Dictionary<string, object?>? data = null;

        if (!string.IsNullOrWhiteSpace(
                notification.DataJson))
        {
            try
            {
                data =
                    JsonSerializer.Deserialize<
                        Dictionary<string, object?>>(
                        notification.DataJson);
            }
            catch
            {
                data = null;
            }
        }

        return new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            Type = notification.Type
                .ToString()
                .ToLowerInvariant(),
            Title = notification.Title,
            Message = notification.Message,
            Data = data,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt,
            ExpiresAt = notification.ExpiresAt,
            ActionUrl = notification.ActionUrl,
            ActionLabel = notification.ActionLabel
        };
    }
}