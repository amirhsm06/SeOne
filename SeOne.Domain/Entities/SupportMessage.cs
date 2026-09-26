namespace SeOne.Domain.Entities;

public class SupportMessage
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public bool IsFromSupport { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string> Attachments { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public bool IsInternal { get; set; }

    public SupportTicket Ticket { get; set; } = null!;
    public User? User { get; set; }
}
