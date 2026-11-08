namespace SeOne.Application.DTOs;

public sealed class CreateGroupMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public bool IsAnnouncement { get; set; }
}

public sealed class CreateClassGroupMessageRequest
{
    public string Content { get; set; } = string.Empty;
}

public sealed class GroupChatMessageDto
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsAnnouncement { get; set; }
}

public sealed class ClassGroupDto
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string GroupType { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ClassGroupMessageDto
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
