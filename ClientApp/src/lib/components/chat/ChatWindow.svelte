<script lang="ts">
    import { onMount, afterUpdate, createEventDispatcher } from 'svelte';
    import type { LHChatConversationDTO, ILHChatMessageDTO } from '$lib/api/ApiClient';
    import {
        currentConversation,
        isLoadingMessages,
        isSendingMessage,
        sendMessage,
        createConversation,
        selectConversation
    } from '$lib/stores/chatStore';
    import ChatMessage from './ChatMessage.svelte';
    import ChatInput from './ChatInput.svelte';

    export let sidebarOpen: boolean = true;

    const dispatch = createEventDispatcher();

    let messagesContainer: HTMLDivElement;
    let shouldAutoScroll = true;

    function handleToggleSidebar() {
        dispatch('toggleSidebar');
    }

    // Determine if a message is from the user (odd index = user, even index = AI in alternating pattern)
    // Since API returns AI response, we need to track which messages are user messages
    function isUserMessage(index: number, totalMessages: number): boolean {
        // Messages alternate: user, AI, user, AI...
        // Index 0 = user, 1 = AI, 2 = user, etc.
        return index % 2 === 0;
    }

    function scrollToBottom(behavior: ScrollBehavior = 'smooth') {
        if (messagesContainer && shouldAutoScroll) {
            messagesContainer.scrollTo({
                top: messagesContainer.scrollHeight,
                behavior
            });
        }
    }

    function handleScroll() {
        if (messagesContainer) {
            const { scrollTop, scrollHeight, clientHeight } = messagesContainer;
            // Auto-scroll if user is near the bottom (within 100px)
            shouldAutoScroll = scrollHeight - scrollTop - clientHeight < 100;
        }
    }

    async function handleSend(event: CustomEvent<string>) {
        const content = event.detail;
        if ($currentConversation) {
            shouldAutoScroll = true;
            await sendMessage($currentConversation.chatConversationID, content);
        }
    }

    async function handleStartConversation(event: CustomEvent<string>) {
        const content = event.detail;
        const newConvo = await createConversation();
        if (newConvo) {
            selectConversation(newConvo);
            // Wait for the conversation to be selected, then send the message
            setTimeout(async () => {
                await sendMessage(newConvo.chatConversationID, content);
            }, 100);
        }
    }

    onMount(() => {
        scrollToBottom('instant');
    });

    afterUpdate(() => {
        scrollToBottom();
    });

    $: messages = $currentConversation?.messages || [];
    $: conversationName = $currentConversation?.name || 'Conversation';
    
    // Placeholder message for loading state
    const loadingMessage: ILHChatMessageDTO = { content: '' };
</script>

<div class="flex flex-col h-full bg-base-200">
    {#if $currentConversation}
        <!-- Header -->
        <div class="flex items-center justify-between px-4 py-3 bg-base-100 border-b border-base-content/10">
            <div class="flex items-center gap-3">
                <!-- Sidebar Toggle Button -->
                <button
                    on:click={handleToggleSidebar}
                    class="btn btn-sm btn-ghost btn-square hover:bg-base-200"
                    title={sidebarOpen ? 'Hide conversations' : 'Show conversations'}
                >
                    {#if sidebarOpen}
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25H12" />
                        </svg>
                    {:else}
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25h16.5" />
                        </svg>
                    {/if}
                </button>
                <div class="w-px h-6 bg-base-content/10"></div>
                <div class="flex items-center gap-2">
                    <div class="w-8 h-8 rounded-lg bg-base-200 flex items-center justify-center">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-4 h-4 text-base-content/70">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M8.625 12a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Zm0 0H8.25m4.125 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Zm0 0H12m4.125 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Zm0 0h-.375M21 12c0 4.556-4.03 8.25-9 8.25a9.764 9.764 0 0 1-2.555-.337A5.972 5.972 0 0 1 5.41 20.97a5.969 5.969 0 0 1-.474-.065 4.48 4.48 0 0 0 .978-2.025c.09-.457-.133-.901-.467-1.226C3.93 16.178 3 14.189 3 12c0-4.556 4.03-8.25 9-8.25s9 3.694 9 8.25Z" />
                        </svg>
                    </div>
                    <div>
                        <h2 class="font-medium text-sm">{conversationName}</h2>
                        <p class="text-xs text-base-content/50">Assistant</p>
                    </div>
                </div>
            </div>
        </div>

        <!-- Messages Area -->
        <div
            bind:this={messagesContainer}
            on:scroll={handleScroll}
            class="flex-1 overflow-y-auto px-4 sm:px-6 py-6"
        >
            {#if $isLoadingMessages}
                <div class="flex items-center justify-center h-full">
                    <span class="loading loading-spinner loading-lg text-base-content/30"></span>
                </div>
            {:else if messages.length === 0}
                <div class="flex flex-col items-center justify-center h-full text-center px-4">
                    <div class="w-14 h-14 rounded-xl bg-base-100 flex items-center justify-center mb-4 border border-base-content/10">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-6 h-6 text-base-content/40">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M8.625 12a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Zm0 0H8.25m4.125 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Zm0 0H12m4.125 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Zm0 0h-.375M21 12c0 4.556-4.03 8.25-9 8.25a9.764 9.764 0 0 1-2.555-.337A5.972 5.972 0 0 1 5.41 20.97a5.969 5.969 0 0 1-.474-.065 4.48 4.48 0 0 0 .978-2.025c.09-.457-.133-.901-.467-1.226C3.93 16.178 3 14.189 3 12c0-4.556 4.03-8.25 9-8.25s9 3.694 9 8.25Z" />
                        </svg>
                    </div>
                    <h3 class="font-medium text-base mb-1">Start the conversation</h3>
                    <p class="text-sm text-base-content/50 max-w-sm">
                        Type a message below to begin.
                    </p>
                </div>
            {:else}
                <div class="max-w-3xl mx-auto">
                    {#each messages as message, index (message.chatMessageItemID || index)}
                        <ChatMessage
                            {message}
                            isUser={isUserMessage(index, messages.length)}
                            isLoading={message.chatMessageItemID?.startsWith('temp-')}
                        />
                    {/each}

                    {#if $isSendingMessage}
                        <ChatMessage
                            message={loadingMessage}
                            isUser={false}
                            isLoading={true}
                        />
                    {/if}
                </div>
            {/if}
        </div>

        <!-- Input Area -->
        <div class="max-w-3xl mx-auto w-full">
            <ChatInput
                on:send={handleSend}
                disabled={$isSendingMessage}
                placeholder="Message your AI assistant..."
            />
        </div>
    {:else}
        <!-- Empty State - No conversation selected -->
        <div class="flex flex-col h-full">
            <!-- Header with toggle -->
            <div class="flex items-center px-4 py-3 bg-base-100 border-b border-base-content/10">
                <button
                    on:click={handleToggleSidebar}
                    class="btn btn-sm btn-ghost btn-square hover:bg-base-200"
                    title={sidebarOpen ? 'Hide conversations' : 'Show conversations'}
                >
                    {#if sidebarOpen}
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25H12" />
                        </svg>
                    {:else}
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25h16.5" />
                        </svg>
                    {/if}
                </button>
            </div>

            <div class="flex-1 flex flex-col items-center justify-center px-4">
                <div class="text-center max-w-md">
                    <div class="w-16 h-16 rounded-2xl bg-base-100 border border-base-content/10 flex items-center justify-center mx-auto mb-6">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-8 h-8 text-base-content/40">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M9.813 15.904 9 18.75l-.813-2.846a4.5 4.5 0 0 0-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 0 0 3.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 0 0 3.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 0 0-3.09 3.09ZM18.259 8.715 18 9.75l-.259-1.035a3.375 3.375 0 0 0-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 0 0 2.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 0 0 2.456 2.456L21.75 6l-1.035.259a3.375 3.375 0 0 0-2.456 2.456ZM16.894 20.567 16.5 21.75l-.394-1.183a2.25 2.25 0 0 0-1.423-1.423L13.5 18.75l1.183-.394a2.25 2.25 0 0 0 1.423-1.423l.394-1.183.394 1.183a2.25 2.25 0 0 0 1.423 1.423l1.183.394-1.183.394a2.25 2.25 0 0 0-1.423 1.423Z" />
                        </svg>
                    </div>
                   
                    <h2 class="text-xl font-semibold tracking-tight mb-2">
                        Lionheart Intelligence
                    </h2>
                    <p class="text-base-content/60 text-sm mb-8">
                        Your AI assistant with access to your training data for intelligent analysis and personalized insights.
                    </p>

                    <!-- Quick Start Input -->
                    <div class="bg-base-100 rounded-xl border border-base-content/10">
                        <ChatInput
                            on:send={handleStartConversation}
                            placeholder="Start a new conversation..."
                        />
                    </div>
                </div>
            </div>
        </div>
    {/if}
</div>
