namespace SeOne.Domain.Entities;

public class NewsletterSubscriber
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Status { get; set; } = "active";
    public DateTime SubscribedAt { get; set; }
    public DateTime? UnsubscribedAt { get; set; }
    public bool Blog { get; set; } = true;
    public bool Courses { get; set; } = true;
    public bool Promotions { get; set; } = true;
    public bool Tips { get; set; } = true;
    public string Source { get; set; } = "website";
    public string ManageToken { get; set; } = string.Empty;
}
