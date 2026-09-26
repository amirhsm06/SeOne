namespace SeOne.Domain.Entities;

public class SupportTicket
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
    public string Priority { get; set; } = "medium";
    public string Status { get; set; } = "open";
    public string Description { get; set; } = string.Empty;
    public List<string> Attachments { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? AssignedToId { get; set; }

    public User? User { get; set; }
    public User? AssignedTo { get; set; }
    public ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
}
