namespace SeOne.Domain.Entities;

public sealed class ClassGroupMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderRole { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
