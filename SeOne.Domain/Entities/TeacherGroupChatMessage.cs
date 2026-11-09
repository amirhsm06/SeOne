namespace SeOne.Domain.Entities;

public sealed class TeacherGroupChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderId { get; set; }
    public string SenderRole { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsAnnouncement { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
