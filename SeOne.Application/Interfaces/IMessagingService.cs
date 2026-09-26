using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface IMessagingService
{
    Task<List<ConversationDto>> GetConversationsAsync(
        Guid userId);

    Task<List<ConversationDto>> SearchConversationsAsync(
        Guid userId,
        string query);

    Task<ConversationDto?> CreateConversationAsync(
        Guid userId,
        CreateConversationRequest request);

    Task<ConversationDto?> GetConversationAsync(
        Guid userId,
        Guid conversationId);

    Task<List<MessageDto>?> GetMessagesAsync(
        Guid userId,
        Guid conversationId);

    Task<MessageDto?> SendMessageAsync(
        Guid userId,
        Guid conversationId,
        SendMessageRequest request);

    Task<bool> MarkConversationAsReadAsync(
        Guid userId,
        Guid conversationId);

    Task<bool> MarkMessageAsReadAsync(
        Guid userId,
        Guid messageId);

    Task<List<TypingStatusDto>?> GetTypingStatusAsync(
        Guid userId,
        Guid conversationId);

    Task<bool> SetTypingStatusAsync(
        Guid userId,
        Guid conversationId,
        bool isTyping);
}