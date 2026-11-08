namespace SeOne.Domain.Entities;

/// <summary>
/// A persistent message in the shared teacher/admin group chat.
/// Access is controlled by TeacherGroupChatController; teachers must have an active profile.
/// </summary>
public sealed class TeacherGroupChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderId { get; set; }
    public string SenderRole { get; set; } = "Teacher";
    public string Content { get; set; } = string.Empty;
    public bool IsAnnouncement { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
