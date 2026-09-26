namespace SeOne.Domain.Entities;

public class NewsletterCampaign
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = "blog";
    public string Status { get; set; } = "draft";
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public int RecipientCount { get; set; }
    public int OpenCount { get; set; }
    public int ClickCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
}
