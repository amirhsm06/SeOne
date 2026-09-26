using System;

namespace SeOne.Domain.Entities;

public class Message
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Guid SenderId { get; set; }

    public string Content { get; set; } = string.Empty;

    public List<string> Attachments { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public User Sender { get; set; } = null!;
}