import { writable, derived, get } from 'svelte/store';
import type { ILHChatMessageDTO } from '$lib/api/ApiClient';
import {
    GetAllChatConversationsEndpointClient,
    GetChatConversationEndpointClient,
    CreateChatConversationEndpointClient,
    DeleteChatConversationEndpointClient,
    ProcessUserChatMessageEndpointClient,
    CreateChatConversationRequest,
    AddChatMessageRequest
} from '$lib/api/ApiClient';

// Local interface that uses interface types for messages (avoids class method issues)
interface ChatConversation {
    chatConversationID: string;
    createdAt: Date;
    lastUpdate: Date;
    name: string | undefined;
    messages: ILHChatMessageDTO[] | undefined;
}

// Store for all conversations (list view)
export const conversations = writable<ChatConversation[]>([]);

// Store for the currently selected conversation (with full messages)
export const currentConversation = writable<ChatConversation | null>(null);

// Loading states
export const isLoadingConversations = writable(false);
export const isLoadingMessages = writable(false);
export const isSendingMessage = writable(false);

// Error state
export const chatError = writable<string | null>(null);

// Derived store for sorted conversations (most recent first)
export const sortedConversations = derived(conversations, ($conversations) => {
    return [...$conversations]
        .filter(c => c != null)
        .sort((a, b) => {
            const dateA = new Date(a.lastUpdate || a.createdAt || 0).getTime();
            const dateB = new Date(b.lastUpdate || b.createdAt || 0).getTime();
            return dateB - dateA;
        });
});

// API actions
export async function fetchConversations(): Promise<void> {
    isLoadingConversations.set(true);
    chatError.set(null);

    try {
        const client = new GetAllChatConversationsEndpointClient('');
        const result = await client.get();
        conversations.set(result || []);
    } catch (error) {
        console.error('Failed to fetch conversations:', error);
        chatError.set('Failed to load conversations');
    } finally {
        isLoadingConversations.set(false);
    }
}

export async function fetchConversation(conversationId: string): Promise<void> {
    isLoadingMessages.set(true);
    chatError.set(null);

    try {
        const client = new GetChatConversationEndpointClient('');
        const result = await client.get(conversationId);
        currentConversation.set(result);
    } catch (error) {
        console.error('Failed to fetch conversation:', error);
        chatError.set('Failed to load conversation');
    } finally {
        isLoadingMessages.set(false);
    }
}

export async function createConversation(name?: string): Promise<ChatConversation | null> {
    chatError.set(null);

    try {
        const client = new CreateChatConversationEndpointClient('');
        const request = new CreateChatConversationRequest({
            name: name || 'New Conversation'
        });
        const result = await client.post(request);

        // Add to conversations list
        conversations.update(convos => [result, ...convos]);

        return result;
    } catch (error) {
        console.error('Failed to create conversation:', error);
        chatError.set('Failed to create conversation');
        return null;
    }
}

export async function deleteConversation(conversationId: string): Promise<boolean> {
    chatError.set(null);

    try {
        const client = new DeleteChatConversationEndpointClient('');
        await client.delete(conversationId);

        // Remove from conversations list
        conversations.update(convos =>
            convos.filter(c => c.chatConversationID !== conversationId)
        );

        // Clear current conversation if it was deleted
        const current = get(currentConversation);
        if (current?.chatConversationID === conversationId) {
            currentConversation.set(null);
        }

        return true;
    } catch (error) {
        console.error('Failed to delete conversation:', error);
        chatError.set('Failed to delete conversation');
        return false;
    }
}

export async function sendMessage(conversationId: string, content: string): Promise<ILHChatMessageDTO | null> {
    isSendingMessage.set(true);
    chatError.set(null);

    try {
        const client = new ProcessUserChatMessageEndpointClient('');
        const request = new AddChatMessageRequest({
            chatConversationID: conversationId,
            content: content
        });

        // Optimistically add user message to UI
        const userMessage: ILHChatMessageDTO = {
            chatMessageItemID: `temp-${Date.now()}`,
            chatConversationID: conversationId,
            creationTime: new Date(),
            content: content,
            tokenCount: 0
        };

        currentConversation.update(convo => {
            if (!convo) return convo;
            return {
                ...convo,
                messages: [...(convo.messages || []), userMessage]
            };
        });

        // Send to API and get AI response
        const aiResponse = await client.post(request);

        // Replace optimistic message and add AI response
        currentConversation.update(convo => {
            if (!convo) return convo;
            const messages = convo.messages || [];
            // Remove temp message and add both real user message context and AI response
            const filteredMessages = messages.filter(m => !m.chatMessageItemID?.startsWith('temp-'));
            return {
                ...convo,
                messages: [...filteredMessages, userMessage, aiResponse],
                lastUpdate: new Date()
            };
        });

        // Update conversation in list
        conversations.update(convos =>
            convos.map(c =>
                c.chatConversationID === conversationId
                    ? { ...c, lastUpdate: new Date() }
                    : c
            )
        );

        return aiResponse;
    } catch (error) {
        console.error('Failed to send message:', error);
        chatError.set('Failed to send message');

        // Remove optimistic message on error
        currentConversation.update(convo => {
            if (!convo) return convo;
            return {
                ...convo,
                messages: (convo.messages || []).filter(m => !m.chatMessageItemID?.startsWith('temp-'))
            };
        });

        return null;
    } finally {
        isSendingMessage.set(false);
    }
}

export function selectConversation(conversation: ChatConversation | null): void {
    currentConversation.set(conversation);
    if (conversation) {
        fetchConversation(conversation.chatConversationID);
    }
}

export function clearCurrentConversation(): void {
    currentConversation.set(null);
}
