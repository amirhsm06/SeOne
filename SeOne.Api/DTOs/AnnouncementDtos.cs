using SeOne.Domain.Entities;

namespace SeOne.Api.DTOs;

public sealed class CreateAnnouncementRequest
{
    public string? Title { get; set; }

    public string? Message { get; set; }

    public string? Audience { get; set; }

    public string? Kind { get; set; }

    public string? Status { get; set; }

    public string? ActionUrl { get; set; }

    public string? ActionLabel { get; set; }

    public DateTimeOffset? PublishAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class AnnouncementResponse
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? ActionUrl { get; init; }

    public string? ActionLabel { get; init; }

    public DateTime? PublishAt { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public bool? IsRead { get; init; }

    public bool? IsDismissed { get; init; }

    public static AnnouncementResponse From(
        Announcement item,
        DateTime nowUtc,
        bool? isRead = null,
        bool? isDismissed = null)
    {
        return new AnnouncementResponse
        {
            Id = item.Id,
            Title = item.Title,
            Message = item.Message,
            Audience = item.Audience,
            Kind = item.Kind,
            Status = AnnouncementContract.EffectiveStatus(item, nowUtc),
            ActionUrl = item.ActionUrl,
            ActionLabel = item.ActionLabel,
            PublishAt = item.PublishAt,
            ExpiresAt = item.ExpiresAt,
            CreatedAt = item.CreatedAt,
            IsRead = isRead,
            IsDismissed = isDismissed
        };
    }
}

internal static class AnnouncementContract
{
    private static readonly HashSet<string> Audiences =
        new(StringComparer.Ordinal) { "students", "teachers", "all" };

    private static readonly HashSet<string> Kinds =
        new(StringComparer.Ordinal) { "info", "success", "warning", "urgent" };

    private static readonly HashSet<string> Statuses =
        new(StringComparer.Ordinal) { "draft", "scheduled", "published" };

    public static string? ValidateAndNormalize(
        Announcement item,
        DateTime nowUtc)
    {
        item.Title = (item.Title ?? string.Empty).Trim();
        item.Message = (item.Message ?? string.Empty).Trim();
        item.Audience = (item.Audience ?? string.Empty).Trim().ToLowerInvariant();
        item.Kind = (item.Kind ?? string.Empty).Trim().ToLowerInvariant();
        item.Status = (item.Status ?? string.Empty).Trim().ToLowerInvariant();
        item.ActionUrl = string.IsNullOrWhiteSpace(item.ActionUrl)
            ? null
            : item.ActionUrl.Trim();
        item.ActionLabel = string.IsNullOrWhiteSpace(item.ActionLabel)
            ? null
            : item.ActionLabel.Trim();

        if (item.PublishAt.HasValue)
        {
            item.PublishAt = NormalizeUtc(item.PublishAt.Value);
        }

        if (item.ExpiresAt.HasValue)
        {
            item.ExpiresAt = NormalizeUtc(item.ExpiresAt.Value);
        }

        if (item.Title.Length == 0 || item.Title.Length > 100)
        {
            return "title is required and must not exceed 100 characters.";
        }

        if (item.Message.Length == 0 || item.Message.Length > 1200)
        {
            return "message is required and must not exceed 1200 characters.";
        }

        if (!Audiences.Contains(item.Audience))
        {
            return "audience must be students, teachers, or all.";
        }

        if (!Kinds.Contains(item.Kind))
        {
            return "kind must be info, success, warning, or urgent.";
        }

        if (!Statuses.Contains(item.Status))
        {
            return "status must be draft, scheduled, or published.";
        }

        if (item.ActionLabel is { Length: > 200 })
        {
            return "actionLabel must not exceed 200 characters.";
        }

        if (item.ActionUrl is { Length: > 1000 })
        {
            return "actionUrl must not exceed 1000 characters.";
        }

        if (!IsSafeActionUrl(item.ActionUrl))
        {
            return "actionUrl must be an HTTPS URL or a same-origin relative path.";
        }

        if (item.Status == "scheduled" &&
            (!item.PublishAt.HasValue || item.PublishAt.Value <= nowUtc))
        {
            return "A scheduled announcement requires a future publishAt.";
        }

        var effectivePublishAt = item.PublishAt ?? nowUtc;
        if (item.ExpiresAt.HasValue && item.ExpiresAt.Value <= effectivePublishAt)
        {
            return "expiresAt must be later than publishAt (or the current time when publishAt is omitted).";
        }

        if (item.Status != "draft" &&
            item.ExpiresAt.HasValue &&
            item.ExpiresAt.Value <= nowUtc)
        {
            return "A non-draft announcement cannot already be expired.";
        }

        return null;
    }

    public static string EffectiveStatus(Announcement item, DateTime nowUtc)
    {
        if (item.Status == "scheduled" &&
            item.PublishAt.HasValue &&
            item.PublishAt.Value <= nowUtc)
        {
            return "published";
        }

        return item.Status;
    }

    public static bool IsSafeActionUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (value.Any(char.IsControl) || value.Contains('\\'))
        {
            return false;
        }

        if (value.StartsWith("/", StringComparison.Ordinal))
        {
            return !value.StartsWith("//", StringComparison.Ordinal);
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(uri.Host) &&
               string.IsNullOrEmpty(uri.UserInfo);
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
