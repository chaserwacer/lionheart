using Ardalis.Result;
using lionheart.Model.DTOs;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services;

public interface IChatConversationService
{
    /// <summary>
    /// Create a new chat conversation.
    /// </summary>
    /// <param name="user">The user who owns the conversation.</param>
    /// <param name="request">The request containing chat conversation details.</param>
    /// <returns>A result containing the created chat conversation.</returns>
    Task<Result<ChatConversationDTO>> CreateChatConversationAsync(IdentityUser user, CreateChatConversationRequest request);

    /// <summary>
    /// Get a chat conversation by ID.
    /// </summary>
    /// <param name="user">The user who owns the conversation.</param>
    /// <param name="conversationId">The conversation ID to retrieve.</param>
    /// <returns>A result containing the requested chat conversation.</returns>
    Task<Result<ChatConversationDTO>> GetChatConversationAsync(IdentityUser user, Guid conversationId);

    /// <summary>
    /// Get all chat conversations for a user.
    /// </summary>
    /// <param name="user">The user whose conversations to retrieve.</param>
    /// <returns>A result containing a list of chat conversations.</returns>
    Task<Result<List<ChatConversationDTO>>> GetAllChatConversationsAsync(IdentityUser user);

    /// <summary>
    /// Update an existing chat conversation.
    /// </summary>
    /// <param name="user">The user who owns the conversation.</param>
    /// <param name="request">The request containing update details.</param>
    /// <returns>A result containing the updated chat conversation.</returns>
    Task<Result<ChatConversationDTO>> UpdateChatConversationAsync(IdentityUser user, UpdateChatConversationRequest request);

    /// <summary>
    /// Delete a chat conversation.
    /// </summary>
    /// <param name="user">The user who owns the conversation.</param>
    /// <param name="request">The request containing the conversation ID to delete.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> DeleteChatConversationAsync(IdentityUser user, DeleteChatConversationRequest request);

    /// <summary>
    /// Add a message to an existing chat conversation.
    /// </summary>
    /// <param name="user">The user who owns the conversation.</param>
    /// <param name="request">The request containing the message details.</param>
    /// <returns>A result containing the updated chat conversation.</returns>
    Task<Result<ChatConversationDTO>> AddChatMessageAsync(IdentityUser user, AddChatMessageRequest request);


    Task<Result<ChatConversation>> GetChatConversationModelVersionAsync(IdentityUser user, Guid conversationId);
}
