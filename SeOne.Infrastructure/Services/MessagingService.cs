using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;


namespace SeOne.Infrastructure.Services;

public class MessagingService : IMessagingService
{
    private readonly SeOneDbContext _context;
    private readonly IMessagingFileStorageService _messagingStorage;
    private readonly ILogger<MessagingService> _logger;
    private readonly INotificationService _notificationService;

    public MessagingService(
        SeOneDbContext context,
        IMessagingFileStorageService messagingStorage,
        INotificationService notificationService,
        ILogger<MessagingService> logger)
    {
        _context = context;
        _messagingStorage = messagingStorage;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<List<MessagingContactDto>> GetMessagingContactsAsync(Guid userId)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return new List<MessagingContactDto>();

        var results = new List<MessagingContactDto>();
        var seen = new HashSet<string>(); // key: "{id}:{courseId}"

        if (user.Role == Domain.Enums.UserRole.Student)
        {
            // Get published courses the student is enrolled in
            var courses = await _context.Set<Enrollment>()
                .AsNoTracking()
                .Where(e => e.StudentId == userId && e.Course.IsPublished)
                .Select(e => new { e.CourseId, e.Course.Title })
                .ToListAsync();

            if (courses.Count > 0)
            {
                var courseIds = courses.Select(c => c.CourseId).ToHashSet();

                var teacherPairs = await _context.Set<CourseInstanceTeacher>()
                    .AsNoTracking()
                    .Where(t => courseIds.Contains(t.CourseInstance.CourseId) && t.Teacher.AccountStatus == "active")
                    .Select(t => new
                    {
                        TeacherId = t.TeacherId,
                        TeacherName = t.Teacher.FullName,
                        Avatar = t.Teacher.AvatarUrl,
                        CourseId = t.CourseInstance.CourseId,
                    CourseTitle = t.CourseInstance.Course == null ? null : t.CourseInstance.Course.Title
                    })
                    .ToListAsync();

                foreach (var t in teacherPairs)
                {
                    var key = $"{t.TeacherId}:{t.CourseId}";
                    if (seen.Add(key) && t.TeacherId != userId)
                    {
                        results.Add(new MessagingContactDto
                        {
                            Id = t.TeacherId,
                            Name = t.TeacherName,
                            Avatar = t.Avatar,
                            Role = GetRoleName(Domain.Enums.UserRole.Teacher),
                            CourseId = t.CourseId,
                            CourseTitle = t.CourseTitle
                        });
                    }
                }
            }
        }
        else if (user.Role == Domain.Enums.UserRole.Teacher)
        {
            // Get courses teacher teaches and students enrolled in them
            var courseIds = await _context.Set<CourseInstanceTeacher>()
                .AsNoTracking()
                .Where(t => t.TeacherId == userId)
                .Select(t => t.CourseInstance.CourseId)
                .Distinct()
                .ToListAsync();

            if (courseIds.Count > 0)
            {
                var students = await _context.Set<Enrollment>()
                    .AsNoTracking()
                    .Where(e => courseIds.Contains(e.CourseId) && e.Student.AccountStatus == "active")
                    .Select(e => new { e.StudentId, e.Student.FullName, Avatar = e.Student.AvatarUrl, e.CourseId, e.Course.Title })
                    .ToListAsync();

                foreach (var s in students)
                {
                    var key = $"{s.StudentId}:{s.CourseId}";
                    if (seen.Add(key) && s.StudentId != userId)
                    {
                        results.Add(new MessagingContactDto
                        {
                            Id = s.StudentId,
                            Name = s.FullName,
                            Avatar = s.Avatar,
                            Role = GetRoleName(Domain.Enums.UserRole.Student),
                            CourseId = s.CourseId,
                            CourseTitle = s.Title
                        });
                    }
                }
            }
        }

        // Support contact configured via SiteSetting "supportEmail"
        var supportSetting = await _context.Set<SiteSetting>().AsNoTracking().FirstOrDefaultAsync(s => s.Key == "supportEmail");
        if (supportSetting is not null && !string.IsNullOrWhiteSpace(supportSetting.Value))
        {
            var supportUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == supportSetting.Value && (u.Role == Domain.Enums.UserRole.Admin || u.Role == Domain.Enums.UserRole.Teacher) && u.AccountStatus == "active");
            if (supportUser is not null && supportUser.Id != userId)
            {
                string key = $"{supportUser.Id}:";
                string key2 = $"{supportUser.Id}:null";
                if (seen.Add(key2))
                {
                    results.Add(new MessagingContactDto
                    {
                        Id = supportUser.Id,
                        Name = supportUser.FullName,
                        Avatar = supportUser.AvatarUrl,
                        Role = GetRoleName(supportUser.Role),
                        CourseId = null,
                        CourseTitle = null
                    });
                }
            }
        }

        return results;
    }

    public async Task<List<ConversationDto>>
        GetConversationsAsync(Guid userId)
    {
        var conversationIds =
            await GetUserConversationIdsAsync(userId);

        var conversations =
            await _context.Set<Conversation>()
                .AsNoTracking()
                .Where(x =>
                    conversationIds.Contains(x.Id))
                .Include(x => x.Participants)
                    .ThenInclude(x => x.User)
                .Include(x => x.Messages)
                    .ThenInclude(x => x.Sender)
                .OrderByDescending(x => x.UpdatedAt)
                .ToListAsync();

        return conversations
            .Select(x =>
                MapConversation(x, userId))
            .ToList();
    }

    public async Task<List<ConversationDto>>
        SearchConversationsAsync(
            Guid userId,
            string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetConversationsAsync(userId);
        }

        query = query.Trim();

        var conversations =
            await _context.Set<Conversation>()
                .AsNoTracking()
                .Where(x =>
                    x.Participants.Any(p =>
                        p.UserId == userId) &&
                    (
                        (x.Subject != null &&
                         x.Subject.Contains(query)) ||
                        x.Participants.Any(p =>
                            p.User.FullName.Contains(query)) ||
                        x.Messages.Any(m =>
                            m.Content.Contains(query))
                    ))
                .Include(x => x.Participants)
                    .ThenInclude(x => x.User)
                .Include(x => x.Messages)
                    .ThenInclude(x => x.Sender)
                .OrderByDescending(x => x.UpdatedAt)
                .ToListAsync();

        return conversations
            .Select(x =>
                MapConversation(x, userId))
            .ToList();
    }

    public async Task<ConversationDto?>
        CreateConversationAsync(
            Guid userId,
            CreateConversationRequest request)
    {
        if (request.ParticipantId == userId)
        {
            return null;
        }

        if (!TryParseConversationType(
                request.Type,
                out var conversationType))
        {
            return null;
        }

        if (request.Subject is not null &&
            request.Subject.Trim().Length > 300)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(
                request.InitialMessage) &&
            request.InitialMessage.Trim().Length > 5000)
        {
            return null;
        }

        var participantExists =
            await _context.Set<User>()
                .AnyAsync(x =>
                    x.Id == request.ParticipantId);

        if (!participantExists)
        {
            return null;
        }

        if (request.RelatedCourseId.HasValue)
        {
            var courseExists =
                await _context.Set<Course>()
                    .AnyAsync(x =>
                        x.Id ==
                        request.RelatedCourseId.Value);

            if (!courseExists)
            {
                return null;
            }
        }

        if (request.RelatedAssignmentId.HasValue)
        {
            var assignmentExists =
                await _context.Set<Assignment>()
                    .AnyAsync(x =>
                        x.Id ==
                        request.RelatedAssignmentId.Value);

            if (!assignmentExists)
            {
                return null;
            }
        }

        // Enforce contact eligibility: participant must appear in the
        // contacts list for the requesting user and, if a related
        // course is specified, the contact must be associated with
        // that course. This prevents creating conversations with
        // arbitrary users.
        var contacts = await GetMessagingContactsAsync(userId);
        var eligible = contacts.Any(c =>
            c.Id == request.ParticipantId &&
            (!request.RelatedCourseId.HasValue || (c.CourseId.HasValue && c.CourseId.Value == request.RelatedCourseId.Value)));

        if (!eligible)
        {
            return null;
        }

        var existing =
            await FindExistingTwoPersonConversationAsync(
                userId,
                request.ParticipantId,
                conversationType);

        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(
                    request.InitialMessage))
            {
                await SendMessageAsync(
                    userId,
                    existing.Id,
                    new SendMessageRequest
                    {
                        Content =
                            request.InitialMessage
                    });
            }

            return await GetConversationAsync(
                userId,
                existing.Id);
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        var now = DateTime.UtcNow;

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = conversationType,
            Subject = string.IsNullOrWhiteSpace(
                request.Subject)
                ? null
                : request.Subject.Trim(),
            RelatedCourseId =
                request.RelatedCourseId,
            RelatedAssignmentId =
                request.RelatedAssignmentId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Set<Conversation>()
            .Add(conversation);

        _context.Set<ConversationParticipant>()
            .AddRange(
                new ConversationParticipant
                {
                    ConversationId =
                        conversation.Id,
                    UserId = userId,
                    JoinedAt = now
                },
                new ConversationParticipant
                {
                    ConversationId =
                        conversation.Id,
                    UserId =
                        request.ParticipantId,
                    JoinedAt = now
                });

        if (!string.IsNullOrWhiteSpace(
                request.InitialMessage))
        {
            _context.Set<Message>()
                .Add(
                    new Message
                    {
                        Id = Guid.NewGuid(),
                        ConversationId =
                            conversation.Id,
                        SenderId = userId,
                        Content =
                            request.InitialMessage.Trim(),
                        Attachments =
                            new List<string>(),
                        CreatedAt = now
                    });
        }

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return await GetConversationAsync(
            userId,
            conversation.Id);
    }

    public async Task<ConversationDto?>
        GetConversationAsync(
            Guid userId,
            Guid conversationId)
    {
        var conversation =
            await _context.Set<Conversation>()
                .AsNoTracking()
                .Include(x => x.Participants)
                    .ThenInclude(x => x.User)
                .Include(x => x.Messages)
                    .ThenInclude(x => x.Sender)
                .FirstOrDefaultAsync(x =>
                    x.Id == conversationId &&
                    x.Participants.Any(p =>
                        p.UserId == userId));

        return conversation is null
            ? null
            : MapConversation(
                conversation,
                userId);
    }

    public async Task<List<MessageDto>?>
        GetMessagesAsync(
            Guid userId,
            Guid conversationId)
    {
        var participant =
            await _context.Set<ConversationParticipant>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ConversationId ==
                    conversationId &&
                    x.UserId == userId);

        if (participant is null)
        {
            return null;
        }

        var messages =
            await _context.Set<Message>()
                .AsNoTracking()
                .Where(x =>
                    x.ConversationId ==
                    conversationId)
                .Include(x => x.Sender)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();

        var list = new List<MessageDto>();
        foreach (var m in messages)
        {
            list.Add(await MapMessageAsync(m, userId, participant.LastReadAt));
        }

        return list;
    }

    public async Task<MessageDto?>
        SendMessageAsync(
            Guid userId,
            Guid conversationId,
            SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(
                request.Content))
        {
            return null;
        }

        if (request.Content.Trim().Length > 5000)
        {
            return null;
        }

        if (!AreAttachmentsValid(
                request.Attachments))
        {
            return null;
        }

        var participant =
            await _context.Set<ConversationParticipant>()
                .FirstOrDefaultAsync(x =>
                    x.ConversationId ==
                    conversationId &&
                    x.UserId == userId);

        if (participant is null)
        {
            return null;
        }

        var conversation =
            await _context.Set<Conversation>()
                .FirstOrDefaultAsync(x =>
                    x.Id == conversationId);

        if (conversation is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = userId,
            Content = request.Content.Trim(),
            Attachments =
                request.Attachments?
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList()
                ?? new List<string>(),
            CreatedAt = now
        };

        _context.Set<Message>()
            .Add(message);

        conversation.UpdatedAt = now;

        await _context.SaveChangesAsync();

        // Notify conversation participants other than the sender.
        var recipientIds = await _context.Set<ConversationParticipant>()
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId && x.UserId != userId)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync();

        if (recipientIds.Count > 0)
        {
            var senderName = await _context.Users
                .AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => x.FullName)
                .FirstOrDefaultAsync() ?? "Someone";

            foreach (var recipientId in recipientIds)
            {
                try
                {
                    await _notificationService.CreateAsync(
                        recipientId,
                        "Message",
                        $"New message from {senderName}",
                        "You have received a new message.",
                        new Dictionary<string, object?>
                        {
                            ["conversationId"] = conversationId,
                            ["messageId"] = message.Id,
                            ["senderId"] = userId
                        });
                }
                catch (Exception exception)
                {
                    // The message is already persisted. Do not turn a
                    // notification failure into a failed message response.
                    _logger.LogWarning(
                        exception,
                        "Could not create in-app notification for message {MessageId} and recipient {RecipientId}.",
                        message.Id,
                        recipientId);
                }
            }
        }

        return await _context.Set<Message>()
            .AsNoTracking()
            .Include(x => x.Sender)
            .Where(x => x.Id == message.Id)
            .Select(x =>
                new MessageDto
                {
                    Id = x.Id,
                    ConversationId =
                        x.ConversationId,
                    SenderId = x.SenderId,
                    SenderName =
                        x.Sender.FullName,
                    SenderAvatar = null,
                    SenderRole =
                        GetRoleName(x.Sender.Role),
                    Content = x.Content,
                    Attachments =
                        x.Attachments,
                    CreatedAt = x.CreatedAt,
                    IsRead = true,
                    ReadAt = null
                })
            .FirstAsync();
    }

    public async Task<bool>
        MarkConversationAsReadAsync(
            Guid userId,
            Guid conversationId)
    {
        var participant =
            await _context
                .Set<ConversationParticipant>()
                .FirstOrDefaultAsync(x =>
                    x.ConversationId ==
                    conversationId &&
                    x.UserId == userId);

        if (participant is null)
        {
            return false;
        }

        participant.LastReadAt =
            DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool>
        MarkMessageAsReadAsync(
            Guid userId,
            Guid messageId)
    {
        var message =
            await _context.Set<Message>()
                .FirstOrDefaultAsync(x =>
                    x.Id == messageId);

        if (message is null)
        {
            return false;
        }

        var participant =
            await _context
                .Set<ConversationParticipant>()
                .FirstOrDefaultAsync(x =>
                    x.ConversationId ==
                    message.ConversationId &&
                    x.UserId == userId);

        if (participant is null)
        {
            return false;
        }

        if (!participant.LastReadAt.HasValue ||
            participant.LastReadAt.Value <
            message.CreatedAt)
        {
            participant.LastReadAt =
                message.CreatedAt;

            await _context.SaveChangesAsync();
        }

        return true;
    }

    public async Task<List<TypingStatusDto>?>
        GetTypingStatusAsync(
            Guid userId,
            Guid conversationId)
    {
        var participant =
            await _context
                .Set<ConversationParticipant>()
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ConversationId ==
                    conversationId &&
                    x.UserId == userId);

        if (!participant)
        {
            return null;
        }

        // Real-time typing state will be handled by
        // SignalR later. HTTP fallback returns no active
        // typing users.
        return new List<TypingStatusDto>();
    }

    public async Task<bool>
        SetTypingStatusAsync(
            Guid userId,
            Guid conversationId,
            bool isTyping)
    {
        var participant =
            await _context
                .Set<ConversationParticipant>()
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ConversationId ==
                    conversationId &&
                    x.UserId == userId);

        if (!participant)
        {
            return false;
        }

        // SignalR will provide the actual real-time
        // typing implementation later.
        return true;
    }

    private async Task<List<Guid>>
        GetUserConversationIdsAsync(
            Guid userId)
    {
        return await _context
            .Set<ConversationParticipant>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.ConversationId)
            .ToListAsync();
    }

    private async Task<Conversation?>
        FindExistingTwoPersonConversationAsync(
            Guid userId,
            Guid participantId,
            ConversationType type)
    {
        return await _context
            .Set<Conversation>()
            .Include(x => x.Participants)
            .Where(x =>
                x.Type == type &&
                x.Participants.Count == 2 &&
                x.Participants.Any(p =>
                    p.UserId == userId) &&
                x.Participants.Any(p =>
                    p.UserId == participantId))
            .FirstOrDefaultAsync();
    }

    private static ConversationDto
        MapConversation(
            Conversation conversation,
            Guid currentUserId)
    {
        var participant =
            conversation.Participants
                .FirstOrDefault(x =>
                    x.UserId == currentUserId);

        var lastMessage =
            conversation.Messages
                .OrderByDescending(
                    x => x.CreatedAt)
                .FirstOrDefault();

        var unreadCount =
            conversation.Messages.Count(x =>
                x.SenderId != currentUserId &&
                (!participant?.LastReadAt.HasValue ?? true ||
                 participant!.LastReadAt!.Value <
                 x.CreatedAt));

        return new ConversationDto
        {
            Id = conversation.Id,

            Participants =
                conversation.Participants
                    .Select(x =>
                        new ConversationParticipantDto
                        {
                            Id = x.UserId,
                            Name = x.User.FullName,
                            Avatar = null,
                            Role =
                                GetRoleName(
                                    x.User.Role)
                        })
                    .ToList(),

            LastMessage =
                lastMessage is null
                    ? null
                    : new LastMessageDto
                    {
                        Content =
                            lastMessage.Content,
                        CreatedAt =
                            lastMessage.CreatedAt,
                        SenderName =
                            lastMessage.Sender.FullName
                    },

            UnreadCount = unreadCount,

            CreatedAt =
                conversation.CreatedAt,

            UpdatedAt =
                conversation.UpdatedAt,

            Subject =
                conversation.Subject,

            Type =
                GetConversationTypeName(
                    conversation.Type),

            RelatedCourseId =
                conversation.RelatedCourseId,

            RelatedAssignmentId =
                conversation.RelatedAssignmentId
        };
    }

    private async Task<MessageDto>
        MapMessageAsync(
            Message message,
            Guid currentUserId,
            DateTime? lastReadAt)
    {
        var isSender =
            message.SenderId ==
            currentUserId;

        var isRead =
            isSender ||
            (lastReadAt.HasValue &&
             message.CreatedAt <=
             lastReadAt.Value);

        var dto = new MessageDto
        {
            Id = message.Id,
            ConversationId =
                message.ConversationId,
            SenderId =
                message.SenderId,
            SenderName =
                message.Sender.FullName,
            SenderAvatar = null,
            SenderRole =
                GetRoleName(
                    message.Sender.Role),
            Content =
                message.Content,
            // Attachments will be resolved into signed URLs when possible
            Attachments = new List<string>(),
            CreatedAt =
                message.CreatedAt,
            IsRead = isRead,
            ReadAt =
                isRead &&
                !isSender
                    ? lastReadAt
                    : null
        };

        if (message.Attachments is not null)
{
    foreach (var attachment in message.Attachments)
    {
        if (string.IsNullOrWhiteSpace(attachment))
            continue;

        if (Uri.TryCreate(attachment, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme is "https" or "http" &&
                string.IsNullOrEmpty(uri.UserInfo))
            {
                dto.Attachments.Add(attachment);
            }

            continue;
        }

        if (!IsExpectedMessagingObjectPath(
                attachment,
                message.ConversationId,
                message.Id))
        {
            _logger.LogWarning(
                "Skipped an invalid attachment reference for message {MessageId}.",
                message.Id);

            continue;
        }

        try
        {
            var signedUrl = await _messagingStorage
                .CreateSignedDownloadUrlAsync(attachment, 300);

            if (!string.IsNullOrWhiteSpace(signedUrl))
            {
                dto.Attachments.Add(signedUrl);
            }
            else
            {
                _logger.LogWarning(
                    "Could not sign an attachment for message {MessageId}.",
                    message.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not resolve an attachment for message {MessageId}.",
                message.Id);
        }
    }
}

        return dto;
    }
    private static bool IsExpectedMessagingObjectPath(
    string path,
    Guid conversationId,
    Guid messageId)
    {
        var segments = path.Split('/');

        if (segments.Length != 4 ||
            segments[0] != "private-messaging" ||
            !Guid.TryParseExact(segments[1], "D", out var pathConversationId) ||
            !Guid.TryParseExact(segments[2], "D", out var pathMessageId) ||
            pathConversationId != conversationId ||
            pathMessageId != messageId)
        {
            return false;
        }

        var fileName = segments[3];
        var extension = Path.GetExtension(fileName);

        var allowedExtensions = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".gif",
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt",
        ".mp3", ".m4a", ".wav", ".mp4", ".zip"
    };

        if (!allowedExtensions.Contains(extension))
            return false;

        var fileId = fileName[..^extension.Length];

        return Guid.TryParseExact(fileId, "N", out var parsedId) &&
               string.Equals(
                   fileId,
                   parsedId.ToString("N"),
                   StringComparison.Ordinal);
    }

    private static bool
        TryParseConversationType(
            string? value,
            out ConversationType result)
    {
        result = value?.ToLowerInvariant() switch
        {
            "student_teacher" =>
                ConversationType.StudentTeacher,

            "student_support" =>
                ConversationType.StudentSupport,

            "teacher_support" =>
                ConversationType.TeacherSupport,

            "admin" =>
                ConversationType.Admin,

            _ => default
        };

        return !string.IsNullOrWhiteSpace(value) &&
               Enum.IsDefined(
                   typeof(ConversationType),
                   result);
    }

    private static string
        GetConversationTypeName(
            ConversationType type)
    {
        return type switch
        {
            ConversationType.StudentTeacher =>
                "student_teacher",

            ConversationType.StudentSupport =>
                "student_support",

            ConversationType.TeacherSupport =>
                "teacher_support",

            ConversationType.Admin =>
                "admin",

            _ => "student_teacher"
        };
    }

    private static string GetRoleName(
        SeOne.Domain.Enums.UserRole role)
    {
        return role switch
        {
            SeOne.Domain.Enums.UserRole.Teacher =>
                "teacher",

            _ => "student"
        };
    }

    private static bool AreAttachmentsValid(
        List<string>? attachments)
    {
        if (attachments is null)
        {
            return true;
        }

        if (attachments.Count > 20)
        {
            return false;
        }

        return attachments.All(x =>
            !string.IsNullOrWhiteSpace(x) &&
            x.Length <= 1000);
    }
}