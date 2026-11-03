using System.Diagnostics;
using lionheart.Data;
using lionheart.Model.Chat;

namespace lionheart.Services.Chat;

/// <summary>
/// The two things both chats — standalone conversations and the continuing thread — need from the
/// conversation tables.
/// </summary>
internal static class ChatStorage
{
    extension(ModelContext context)
    {
        /// <summary>
        /// The athlete's own conversations, without the ones that are sessions of the thread. A
        /// thread session is written and read through the thread, never renamed, deleted or posted
        /// into as a conversation of its own.
        /// </summary>
        public IQueryable<LHChatConversation> StandaloneConversations =>
            context.ChatConversations.Where(conversation =>
                !context.ChatThreadSessions.Any(session => session.ChatConversationId == conversation.ChatConversationID));

        /// <summary>Adds each message to the table its kind is stored in. Nothing is saved.</summary>
        public void StageChatMessages(IEnumerable<LHChatMessage> messages)
        {
            foreach (var message in messages)
            {
                switch (message)
                {
                    case LHUserChatMessage user:
                        context.UserChatMessages.Add(user);
                        break;
                    case LHModelChatMessage model:
                        context.ModelChatMessages.Add(model);
                        break;
                    case LHChatToolCallResult tool:
                        context.ToolChatMessages.Add(tool);
                        break;
                    case LHSystemChatMessage system:
                        context.SystemChatMessages.Add(system);
                        break;
                    default:
                        throw new UnreachableException($"Unmapped stored chat message type {message.GetType().Name}.");
                }
            }
        }
    }
}
