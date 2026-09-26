using Microsoft.EntityFrameworkCore;
using SeOne.Application.DTOs;
using SeOne.Application.Interfaces;
using SeOne.Domain.Entities;
using SeOne.Domain.Enums;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Infrastructure.Services;

public class MessagingService : IMessagingService
{
    private readonly SeOneDbContext _context;

    public MessagingService(
        SeOneDbContext context)
    {
        _context = context;
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

        return messages
            .Select(x =>
                MapMessage(
                    x,
                    userId,
                    participant.LastReadAt))
            .ToList();
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

    private static MessageDto
        MapMessage(
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

        return new MessageDto
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
            Attachments =
                message.Attachments,
            CreatedAt =
                message.CreatedAt,
            IsRead = isRead,
            ReadAt =
                isRead &&
                !isSender
                    ? lastReadAt
                    : null
        };
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