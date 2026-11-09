namespace SeOne.Domain.Entities;

public sealed class ClassGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string GroupType { get; set; } = "public";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
