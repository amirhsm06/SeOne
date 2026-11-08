namespace SeOne.Domain.Entities;

/// <summary>Membership is kept separately so access can be revoked when enrollment is cancelled.</summary>
public sealed class ClassGroupMember
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Student";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
