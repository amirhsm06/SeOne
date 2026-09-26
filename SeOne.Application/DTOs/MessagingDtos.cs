using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SeOne.Application.DTOs;

public class ConversationParticipantDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Avatar { get; set; }

    public string Role { get; set; } = "student";
}

public class LastMessageDto
{
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string SenderName { get; set; } = string.Empty;
}

public class ConversationDto
{
    public Guid Id { get; set; }

    public List<ConversationParticipantDto> Participants { get; set; }
        = new();

    public LastMessageDto? LastMessage { get; set; }

    public int UnreadCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? Subject { get; set; }

    public string Type { get; set; } = "student_teacher";

    public Guid? RelatedCourseId { get; set; }

    public Guid? RelatedAssignmentId { get; set; }
}

public class MessageDto
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Guid SenderId { get; set; }

    public string SenderName { get; set; } = string.Empty;

    public string? SenderAvatar { get; set; }

    public string SenderRole { get; set; } = "student";

    public string Content { get; set; } = string.Empty;

    public List<string> Attachments { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }
}

public class CreateConversationRequest
{
    [Required]
    public Guid ParticipantId { get; set; }

    public string? Subject { get; set; }

    [Required]
    public string Type { get; set; } = "student_teacher";

    public Guid? RelatedCourseId { get; set; }

    public Guid? RelatedAssignmentId { get; set; }

    [MaxLength(5000)]
    public string? InitialMessage { get; set; }
}

public class SendMessageRequest
{
    [Required]
    [MaxLength(5000)]
    public string Content { get; set; } = string.Empty;

    public List<string>? Attachments { get; set; }
}

public class TypingStatusDto
{
    public Guid UserId { get; set; }

    public bool IsTyping { get; set; }
}

public class SetTypingStatusRequest
{
    public bool IsTyping { get; set; }
}