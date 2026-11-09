namespace SeOne.Domain.Entities;

public sealed class ClassGroupMember
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
