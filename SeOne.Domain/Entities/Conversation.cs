using System;
using SeOne.Domain.Enums;

namespace SeOne.Domain.Entities;

public class Conversation
{
    public Guid Id { get; set; }

    public ConversationType Type { get; set; }

    public string? Subject { get; set; }

    public Guid? RelatedCourseId { get; set; }

    public Guid? RelatedAssignmentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Course? RelatedCourse { get; set; }

    public Assignment? RelatedAssignment { get; set; }

    public ICollection<ConversationParticipant> Participants { get; set; }
        = new List<ConversationParticipant>();

    public ICollection<Message> Messages { get; set; }
        = new List<Message>();
}