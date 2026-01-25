<script lang="ts">
    import type { LHChatConversationDTO } from '$lib/api/ApiClient';
    import {
        sortedConversations,
        currentConversation,
        isLoadingConversations,
        selectConversation,
        createConversation,
        deleteConversation
    } from '$lib/stores/chatStore';

    let isCreating = false;
    let deleteConfirmId: string | null = null;

    async function handleNewConversation() {
        isCreating = true;
        const newConvo = await createConversation();
        if (newConvo) {
            selectConversation(newConvo);
        }
        isCreating = false;
    }

    async function handleDelete(e: Event, conversationId: string) {
        e.stopPropagation();
        if (deleteConfirmId === conversationId) {
            await deleteConversation(conversationId);
            deleteConfirmId = null;
        } else {
            deleteConfirmId = conversationId;
            // Auto-reset after 3 seconds
            setTimeout(() => {
                if (deleteConfirmId === conversationId) {
                    deleteConfirmId = null;
                }
            }, 3000);
        }
    }

    function formatDate(date: Date): string {
        const now = new Date();
        const messageDate = new Date(date);
        const diffMs = now.getTime() - messageDate.getTime();
        const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

        if (diffDays === 0) {
            return messageDate.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        } else if (diffDays === 1) {
            return 'Yesterday';
        } else if (diffDays < 7) {
            return messageDate.toLocaleDateString([], { weekday: 'short' });
        } else {
            return messageDate.toLocaleDateString([], { month: 'short', day: 'numeric' });
        }
    }
</script>

<div class="flex flex-col h-full">
    <!-- Header -->
    <div class="p-4 border-b border-base-content/10">
        <div class="flex items-center justify-between">
            <h2 class="text-sm font-semibold tracking-tight">Conversations</h2>
            <button
                on:click={handleNewConversation}
                disabled={isCreating}
                class="btn btn-sm btn-ghost btn-square hover:bg-base-200"
                title="New conversation"
            >
                {#if isCreating}
                    <span class="loading loading-spinner loading-xs"></span>
                {:else}
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-4 h-4">
                        <path stroke-linecap="round" stroke-linejoin="round" d="m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L10.582 16.07a4.5 4.5 0 0 1-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 0 1 1.13-1.897l8.932-8.931Zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0 1 15.75 21H5.25A2.25 2.25 0 0 1 3 18.75V8.25A2.25 2.25 0 0 1 5.25 6H10" />
                    </svg>
                {/if}
            </button>
        </div>
    </div>

    <!-- Conversations List -->
    <div class="flex-1 overflow-y-auto">
        {#if $isLoadingConversations}
            <div class="flex items-center justify-center py-12">
                <span class="loading loading-spinner loading-sm text-base-content/30"></span>
            </div>
        {:else if $sortedConversations.length === 0}
            <div class="flex flex-col items-center justify-center py-12 px-4 text-center">
                <div class="w-10 h-10 rounded-lg bg-base-200 flex items-center justify-center mb-3">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5 text-base-content/30">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M20.25 8.511c.884.284 1.5 1.128 1.5 2.097v4.286c0 1.136-.847 2.1-1.98 2.193-.34.027-.68.052-1.02.072v3.091l-3-3c-1.354 0-2.694-.055-4.02-.163a2.115 2.115 0 0 1-.825-.242m9.345-8.334a2.126 2.126 0 0 0-.476-.095 48.64 48.64 0 0 0-8.048 0c-1.131.094-1.976 1.057-1.976 2.192v4.286c0 .837.46 1.58 1.155 1.951m9.345-8.334V6.637c0-1.621-1.152-3.026-2.76-3.235A48.455 48.455 0 0 0 11.25 3c-2.115 0-4.198.137-6.24.402-1.608.209-2.76 1.614-2.76 3.235v6.226c0 1.621 1.152 3.026 2.76 3.235.577.075 1.157.14 1.74.194V21l4.155-4.155" />
                    </svg>
                </div>
                <p class="text-base-content/50 text-xs mb-3">No conversations yet</p>
                <button
                    on:click={handleNewConversation}
                    disabled={isCreating}
                    class="btn btn-xs btn-ghost text-xs"
                >
                    Start a conversation
                </button>
            </div>
        {:else}
            <div class="py-1">
                {#each $sortedConversations as conversation (conversation.chatConversationID)}
                    <div class="group relative">
                        <button
                            on:click={() => selectConversation(conversation)}
                            class="w-full text-left px-3 py-2.5 transition-colors duration-100
                                   {$currentConversation?.chatConversationID === conversation.chatConversationID
                                       ? 'bg-base-200'
                                       : 'hover:bg-base-200/50'}"
                        >
                            <div class="flex items-start gap-2 pr-8">
                                <div class="flex-1 min-w-0">
                                    <h3 class="font-medium text-sm truncate leading-tight">
                                        {conversation.name || 'Untitled'}
                                    </h3>
                                    <p class="text-xs text-base-content/40 truncate mt-0.5">
                                        {formatDate(conversation.lastUpdate || conversation.createdAt)}
                                    </p>
                                </div>
                            </div>
                        </button>
                        <!-- Delete button - always accessible -->
                        <button
                            on:click={(e) => handleDelete(e, conversation.chatConversationID)}
                            class="absolute right-2 top-1/2 -translate-y-1/2 btn btn-ghost btn-xs btn-square
                                   opacity-0 group-hover:opacity-100 transition-opacity
                                   {deleteConfirmId === conversation.chatConversationID ? 'opacity-100 !bg-error/10 !text-error' : 'hover:bg-base-300'}"
                            title={deleteConfirmId === conversation.chatConversationID ? 'Click again to confirm delete' : 'Delete conversation'}
                        >
                            {#if deleteConfirmId === conversation.chatConversationID}
                                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-3.5 h-3.5">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                                </svg>
                            {:else}
                                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-3.5 h-3.5">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="m14.74 9-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 0 1-2.244 2.077H8.084a2.25 2.25 0 0 1-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 0 0-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 0 1 3.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 0 0-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 0 0-7.5 0" />
                                </svg>
                            {/if}
                        </button>
                    </div>
                {/each}
            </div>
        {/if}
    </div>
</div>
