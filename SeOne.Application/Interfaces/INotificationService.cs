using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface INotificationService
{
    Task<List<NotificationDto>> GetAsync(
        Guid userId,
        string? type,
        bool unreadOnly,
        int? limit,
        int? offset);

    Task<NotificationStatsDto> GetStatsAsync(
        Guid userId);

    Task<NotificationDto?> GetByIdAsync(
        Guid userId,
        Guid notificationId);

    Task<bool> MarkAsReadAsync(
        Guid userId,
        Guid notificationId);

    Task MarkAllAsReadAsync(
        Guid userId);

    Task<bool> DeleteAsync(
        Guid userId,
        Guid notificationId);

    Task ClearAsync(
        Guid userId,
        bool readOnly,
        DateTime? olderThan);

    Task<NotificationActionResultDto?> ExecuteActionAsync(
        Guid userId,
        Guid notificationId,
        Dictionary<string, object?>? actionData);

    Task<NotificationDto?> CreateTestAsync(
        Guid userId,
        string type);

    Task<NotificationDto> CreateAsync(
        Guid userId,
        string type,
        string title,
        string message,
        Dictionary<string, object?>? data = null,
        string? actionUrl = null,
        string? actionLabel = null,
        DateTime? expiresAt = null);
}