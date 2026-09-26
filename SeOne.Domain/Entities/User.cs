using Microsoft.AspNetCore.Identity;
using SeOne.Domain.Enums;

namespace SeOne.Domain.Entities;

public class User : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;

    public string FamilyName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Bio { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Country { get; set; }

    public string Language { get; set; } = "en";

    public string Timezone { get; set; } = "UTC";

    public string? NotificationPreferencesJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string AccountStatus { get; set; } = "active";

    public string? TwoFactorMethod { get; set; }

    public string? TwoFactorBackupCodesJson { get; set; }

    public string? AccountDeletionToken { get; set; }

    public DateTime? AccountDeletionScheduledAt { get; set; }
}
