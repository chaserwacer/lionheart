<script lang="ts">
    import { onMount } from "svelte";
    import { get, writable } from "svelte/store";
    import { fetchBootUserDto, bootUserDto } from "$lib/stores/stores";
    import {
        ProcessUserChatMessageEndpointClient,
        AddChatMessageRequest,
        CreateChatConversationRequest,
        CreateChatConversationEndpointClient,
        GetAllChatConversationsEndpointClient,
        GetChatConversationEndpointClient,
        DeleteChatConversationEndpointClient,
        LHChatMessageDTO,
        type LHChatConversationDTO,
    } from "$lib/api/ApiClient";

    const baseUrl = "";

    let newMessage = "";
    let loading = false;
    let chatContainer: HTMLElement;

    // Chat conversations (using stores for proper reactivity)
    const conversations = writable<LHChatConversationDTO[]>([]);
    const currentChatConversation = writable<LHChatConversationDTO | null>(null);
    const selectedChatId = writable<string | null>(null);
    let loadingConversations = true;


    function delay(ms: number) {
        return new Promise<void>((resolve) => setTimeout(resolve, ms));
    }

    // Modal state
    let conversationsModalOpen = false;

    function openConversationsModal() {
        conversationsModalOpen = true;
        loadConversations();
    }

    function closeConversationsModal() {
        conversationsModalOpen = false;
    }

    // Load all user conversations
    async function loadConversations() {
        try {
            loadingConversations = true;
            const client = new GetAllChatConversationsEndpointClient(baseUrl);
            conversations.set(await client.get());
            loadingConversations = false;
        } catch (error) {
            console.error("Error loading conversations:", error);
            loadingConversations = false;
        }
    }

    // Load a specific conversation
    async function loadConversation(chatId: string) {
        try {
            const client = new GetChatConversationEndpointClient(baseUrl);
            const conversation = await client.get(chatId);
            currentChatConversation.set(conversation);
            scrollToBottom();
        } catch (error) {
            console.error("Error loading conversation:", error);
            currentChatConversation.set(null);
        }
    }

    // Create a new conversation
    async function createNewConversation(initialMessage: string) {
        if (!initialMessage.trim()) return;

        try {
            loading = true;
            const createRequest = new CreateChatConversationRequest({
                name:
                    initialMessage.length > 30
                        ? initialMessage.substring(0, 27) + "..."
                        : initialMessage,
            });

            const createClient = new CreateChatConversationEndpointClient(
                baseUrl,
            );
            const newConversation = await createClient.post(createRequest);
            if (!newConversation?.chatConversationID) {
                throw new Error(
                    "CreateChatConversation returned an empty response.",
                );
            }
            const newChatId = newConversation.chatConversationID;

            // Add optimistic user message to the conversation object
            const optimisticMessage = LHChatMessageDTO.fromJS({
                chatMessageItemID: crypto.randomUUID(),
                chatConversationID: newChatId,
                creationTime: new Date().toISOString(),
                content: initialMessage,
            });
            newConversation.messages = [optimisticMessage];

            // Set state all at once
            selectedChatId.set(newChatId);
            selectConversation(newChatId);
            // currentChatConversation.set(newConversation);
            // currentChatConversation.update((conv) => {
            //     if (conv) {
            //         conv.messages = [...(conv.messages ?? []), optimisticMessage];
            //     }
            //     return conv;
            // });
            newMessage = "";


            // Send the message to AI
            await sendMessageToAI(initialMessage, newChatId);
        } catch (error) {
            console.error("Error creating new conversation:", error);
            loading = false;
        }
    }

    // Send a message to an existing conversation
    async function sendMessage() {
        if (!newMessage.trim()) return;

        const userQuery = newMessage.trim();
        newMessage = "";

        // If no conversation selected, create a new one
        if (!$selectedChatId || !$currentChatConversation) {
            await createNewConversation(userQuery);
            return;
        }

        loading = true;

        // Add optimistic user message
        const optimisticMessage = LHChatMessageDTO.fromJS({
            chatMessageItemID: crypto.randomUUID(),
            chatConversationID: $selectedChatId,
            creationTime: new Date().toISOString(),
            content: userQuery,
        });
        currentChatConversation.update(conv => {
            if (conv) {
                conv.messages = [...(conv.messages ?? []), optimisticMessage];
            }
            return conv;
        });

        scrollToBottom();

        await sendMessageToAI(userQuery, $selectedChatId);
    }

    // Actually send message to AI and handle response
    async function sendMessageToAI(content: string, chatIdOverride?: string | null) {
        const chatId = chatIdOverride ?? $selectedChatId;
        if (!chatId) return;

        const localConversation = get(currentChatConversation);
        const localMessageCount = localConversation?.messages?.length ?? 0;

        try {
            const chatClient = new ProcessUserChatMessageEndpointClient(
                baseUrl,
            );
            const request = new AddChatMessageRequest({
                chatConversationID: chatId,
                content: content,
            });
            await chatClient.post(request);

            // Reload the conversation to get messages including AI response.
            // Some backends enqueue processing and return before messages are persisted,
            // so we poll and never overwrite optimistic local messages with an empty server response.
            const getChatClient = new GetChatConversationEndpointClient(baseUrl);
            const maxAttempts = 30;
            const pollDelayMs = 350;

            for (let attempt = 0; attempt < maxAttempts; attempt++) {
                const serverConversation = await getChatClient.get(chatId);
                const serverCount = serverConversation?.messages?.length ?? 0;

                if (serverCount >= localMessageCount) {
                    currentChatConversation.set(serverConversation);
                }

                // If assistant message arrived, stop loading.
                if (serverCount > localMessageCount) {
                    break;
                }

                await delay(pollDelayMs);
            }

            loading = false;
            scrollToBottom();
        } catch (error) {
            console.error("Error sending message:", error);
            loading = false;
        }
    }

    // Select a conversation
    function selectConversation(id: string) {
        selectedChatId.set(id);
        conversationsModalOpen = false;
        loadConversation(id);
    }

    // Start a new chat
    function startNewChat() {
        selectedChatId.set(null);
        currentChatConversation.set(null);
        conversationsModalOpen = false;
    }

    // Delete a conversation
    async function deleteConversation(id: string, ev?: MouseEvent) {
        if (ev) {
            ev.stopPropagation();
            ev.preventDefault();
        }
        const convo = $conversations.find((c) => c.chatConversationID === id);
        const name = convo?.name || "this conversation";
        if (!confirm(`Delete '${name}'? This cannot be undone.`)) return;
        try {
            const client = new DeleteChatConversationEndpointClient(baseUrl);
            await client.delete(id);
            await loadConversations();

            // If we deleted the current one, clear the view
            if ($selectedChatId === id) {
                startNewChat();
            }
        } catch (e) {
            console.error("Failed to delete conversation", e);
            alert("Failed to delete conversation.");
        }
    }

    // Handle Enter key press
    function handleKeydown(event: KeyboardEvent) {
        if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            sendMessage();
        }
    }

    function scrollToBottom() {
        setTimeout(() => {
            if (chatContainer) {
                chatContainer.scrollTop = chatContainer.scrollHeight;
            }
        }, 50);
    }

    function escapeHtml(str: string): string {
        return str
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function formatMessage(text: string | undefined): string {
        if (!text) return "";
        return escapeHtml(text).replace(/\n/g, "<br>");
    }

    function isUserMessage(message: LHChatMessageDTO, index: number): boolean {
        // User messages are at even indices (0, 2, 4...)
        return index % 2 === 0;
    }

    onMount(async () => {
        await fetchBootUserDto(fetch);
        await loadConversations();
    });
</script>

<svelte:head>
    <title>{$currentChatConversation?.name ?? "Chat"} - Lionheart</title>
</svelte:head>

<!-- Conversations Modal -->
{#if conversationsModalOpen}
    <div class="fixed inset-0 z-50 flex items-center justify-center">
        <!-- Backdrop -->
        <button
            class="absolute inset-0 bg-black/60 backdrop-blur-sm"
            on:click={closeConversationsModal}
            aria-label="Close modal"
        ></button>

        <!-- Modal Content -->
        <div
            class="relative bg-base-100 rounded-2xl shadow-2xl w-full max-w-lg mx-4 max-h-[80vh] flex flex-col border-2 border-base-content/10"
        >
            <!-- Modal Header -->
            <div
                class="p-5 border-b-2 border-base-content/10 flex items-center justify-between"
            >
                <h2 class="text-lg font-bold">Conversations</h2>
                <div class="flex items-center gap-2">
                    <button
                        class="btn btn-ghost btn-sm"
                        on:click={startNewChat}
                        aria-label="New chat"
                    >
                        <svg
                            xmlns="http://www.w3.org/2000/svg"
                            fill="none"
                            viewBox="0 0 24 24"
                            stroke-width="2"
                            stroke="currentColor"
                            class="w-5 h-5"
                        >
                            <path
                                stroke-linecap="round"
                                stroke-linejoin="round"
                                d="M12 4.5v15m7.5-7.5h-15"
                            />
                        </svg>
                        New
                    </button>
                    <button
                        class="btn btn-ghost btn-sm btn-square"
                        on:click={closeConversationsModal}
                        aria-label="Close"
                    >
                        <svg
                            xmlns="http://www.w3.org/2000/svg"
                            fill="none"
                            viewBox="0 0 24 24"
                            stroke-width="2"
                            stroke="currentColor"
                            class="w-5 h-5"
                        >
                            <path
                                stroke-linecap="round"
                                stroke-linejoin="round"
                                d="M6 18L18 6M6 6l12 12"
                            />
                        </svg>
                    </button>
                </div>
            </div>

            <!-- Modal Body -->
            <div class="flex-1 overflow-y-auto p-3">
                {#if loadingConversations}
                    <div class="flex justify-center py-12">
                        <span class="loading loading-spinner loading-md"></span>
                    </div>
                {:else if $conversations.length === 0}
                    <div class="py-12 text-center">
                        <div
                            class="w-16 h-16 rounded-full bg-base-200 flex items-center justify-center mx-auto mb-4"
                        >
                            <svg
                                xmlns="http://www.w3.org/2000/svg"
                                fill="none"
                                viewBox="0 0 24 24"
                                stroke-width="1.5"
                                stroke="currentColor"
                                class="w-8 h-8 text-base-content/30"
                            >
                                <path
                                    stroke-linecap="round"
                                    stroke-linejoin="round"
                                    d="M7.5 8.25h9m-9 3H12m-9.75 1.51c0 1.6 1.123 2.994 2.707 3.227 1.129.166 2.27.293 3.423.379.35.026.67.21.865.501L12 21l2.755-4.133a1.14 1.14 0 01.865-.501 48.172 48.172 0 003.423-.379c1.584-.233 2.707-1.626 2.707-3.228V6.741c0-1.602-1.123-2.995-2.707-3.228A48.394 48.394 0 0012 3c-2.392 0-4.744.175-7.043.513C3.373 3.746 2.25 5.14 2.25 6.741v6.018z"
                                />
                            </svg>
                        </div>
                        <p class="text-base-content/50 mb-4">
                            No conversations yet
                        </p>
                        <button
                            class="btn btn-primary btn-sm"
                            on:click={startNewChat}
                        >
                            Start a conversation
                        </button>
                    </div>
                {:else}
                    <div class="space-y-1">
                        {#each $conversations as conversation}
                            <button
                                class="group w-full text-left px-4 py-3 rounded-xl transition-all
                                       border-2 border-transparent hover:bg-base-200
                                       {$selectedChatId ===
                                conversation.chatConversationID
                                    ? 'bg-base-200 border-base-content/10'
                                    : ''}"
                                on:click={() =>
                                    selectConversation(
                                        conversation.chatConversationID,
                                    )}
                            >
                                <div class="flex items-center gap-3">
                                    <svg
                                        xmlns="http://www.w3.org/2000/svg"
                                        fill="none"
                                        viewBox="0 0 24 24"
                                        stroke-width="1.5"
                                        stroke="currentColor"
                                        class="w-5 h-5 text-base-content/30 flex-shrink-0"
                                    >
                                        <path
                                            stroke-linecap="round"
                                            stroke-linejoin="round"
                                            d="M7.5 8.25h9m-9 3H12m-9.75 1.51c0 1.6 1.123 2.994 2.707 3.227 1.129.166 2.27.293 3.423.379.35.026.67.21.865.501L12 21l2.755-4.133a1.14 1.14 0 01.865-.501 48.172 48.172 0 003.423-.379c1.584-.233 2.707-1.626 2.707-3.228V6.741c0-1.602-1.123-2.995-2.707-3.228A48.394 48.394 0 0012 3c-2.392 0-4.744.175-7.043.513C3.373 3.746 2.25 5.14 2.25 6.741v6.018z"
                                        />
                                    </svg>
                                    <div class="flex-1 min-w-0">
                                        <span class="block truncate font-medium"
                                            >{conversation.name}</span
                                        >
                                        <span
                                            class="text-xs text-base-content/40 font-mono"
                                        >
                                            {new Date(
                                                conversation.createdAt,
                                            ).toLocaleDateString("en-US", {
                                                month: "short",
                                                day: "numeric",
                                                year: "numeric",
                                            })}
                                        </span>
                                    </div>
                                    <button
                                        type="button"
                                        class="text-base-content/30 hover:text-error p-1
                                               opacity-0 group-hover:opacity-100 transition-opacity"
                                        on:click={(e) =>
                                            deleteConversation(
                                                conversation.chatConversationID,
                                                e,
                                            )}
                                        aria-label="Delete conversation"
                                    >
                                        <svg
                                            xmlns="http://www.w3.org/2000/svg"
                                            fill="none"
                                            viewBox="0 0 24 24"
                                            stroke-width="2"
                                            stroke="currentColor"
                                            class="w-4 h-4"
                                        >
                                            <path
                                                stroke-linecap="round"
                                                stroke-linejoin="round"
                                                d="m14.74 9-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 01-2.244 2.077H8.084a2.25 2.25 0 01-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 00-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 013.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 00-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 00-7.5 0"
                                            />
                                        </svg>
                                    </button>
                                </div>
                            </button>
                        {/each}
                    </div>
                {/if}
            </div>

            <!-- Modal Footer -->
            <div class="p-4 border-t-2 border-base-content/10">
                <p class="text-xs text-base-content/40 text-center">
                    Lionheart does not give medical advice. See project
                    documentation for details.
                </p>
            </div>
        </div>
    </div>
{/if}

<div class="flex h-[calc(100vh-4rem)] bg-base-200">
    <!-- Main Chat Area -->
    <main class="flex-1 flex flex-col overflow-hidden">
        <!-- Header -->
        <header
            class="px-4 py-3 border-b-2 border-base-content/10 bg-base-100 flex items-center gap-3"
        >
            <button
                class="btn btn-ghost btn-sm btn-square"
                on:click={openConversationsModal}
                aria-label="Open conversations"
            >
                <svg
                    xmlns="http://www.w3.org/2000/svg"
                    fill="none"
                    viewBox="0 0 24 24"
                    stroke-width="2"
                    stroke="currentColor"
                    class="w-5 h-5"
                >
                    <path
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        d="M20.25 8.511c.884.284 1.5 1.128 1.5 2.097v4.286c0 1.136-.847 2.1-1.98 2.193-.34.027-.68.052-1.02.072v3.091l-3-3c-1.354 0-2.694-.055-4.02-.163a2.115 2.115 0 01-.825-.242m9.345-8.334a2.126 2.126 0 00-.476-.095 48.64 48.64 0 00-8.048 0c-1.131.094-1.976 1.057-1.976 2.192v4.286c0 .837.46 1.58 1.155 1.951m9.345-8.334V6.637c0-1.621-1.152-3.026-2.76-3.235A48.455 48.455 0 0011.25 3c-2.115 0-4.198.137-6.24.402-1.608.209-2.76 1.614-2.76 3.235v6.226c0 1.621 1.152 3.026 2.76 3.235.577.075 1.157.14 1.74.194V21l4.155-4.155"
                    />
                </svg>
            </button>
            <h1 class="text-lg font-bold truncate flex-1">
                {$currentChatConversation?.name ?? "-------------------------------------"}
            </h1>
            {#if $currentChatConversation}
                <button
                    class="btn btn-ghost btn-sm"
                    on:click={startNewChat}
                    aria-label="New chat"
                >
                    <svg
                        xmlns="http://www.w3.org/2000/svg"
                        fill="none"
                        viewBox="0 0 24 24"
                        stroke-width="2"
                        stroke="currentColor"
                        class="w-5 h-5"
                    >
                        <path
                            stroke-linecap="round"
                            stroke-linejoin="round"
                            d="M12 4.5v15m7.5-7.5h-15"
                        />
                    </svg>
                </button>
            {/if}
        </header>

        <!-- Chat Messages -->
        <div bind:this={chatContainer} class="flex-1 overflow-y-auto px-4 py-8">
            <div class="max-w-3xl mx-auto w-full">
                {#if $currentChatConversation && $currentChatConversation.messages && $currentChatConversation.messages.length > 0}
                    {#each $currentChatConversation.messages as message, idx}
                        {#if message.content}
                            <div class="mb-8">
                                <div class="flex items-start gap-4">
                                    <!-- Avatar -->
                                    <div class="flex-shrink-0">
                                        {#if isUserMessage(message, idx)}
                                            <div
                                                class="w-8 h-8 rounded-full flex items-center justify-center bg-primary text-primary-content"
                                            >
                                                <span class="text-sm font-bold">
                                                    {($bootUserDto?.name ?? "U")
                                                        .charAt(0)
                                                        .toUpperCase()}
                                                </span>
                                            </div>
                                        {:else}
                                            <div
                                                class="w-8 h-8 rounded-full flex items-center justify-center text-base-content bg-base-content/10"
                                            >
                                                <svg
                                                    xmlns="http://www.w3.org/2000/svg"
                                                    fill="none"
                                                    viewBox="0 0 24 24"
                                                    stroke-width="1.5"
                                                    stroke="currentColor"
                                                    class="w-5 h-5"
                                                >
                                                    <path
                                                        stroke-linecap="round"
                                                        stroke-linejoin="round"
                                                        d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z"
                                                    />
                                                </svg>
                                            </div>
                                        {/if}
                                    </div>

                                    <!-- Message Content -->
                                    <div class="flex-1 min-w-0">
                                        <div
                                            class="text-xs font-bold uppercase tracking-widest text-base-content/50 mb-2"
                                        >
                                            {isUserMessage(message, idx)
                                                ? $bootUserDto?.name || "You"
                                                : "chat"}
                                        </div>
                                        <div
                                            class="prose prose-sm max-w-none text-base-content leading-relaxed
                                                    {isUserMessage(message, idx)
                                                ? 'bg-base-100 rounded-lg p-4 border-2 border-base-content/5'
                                                : ''}"
                                        >
                                            {@html formatMessage(
                                                message.content,
                                            )}
                                        </div>
                                    </div>
                                </div>
                            </div>
                        {/if}
                    {/each}
                {:else}
                    <!-- Empty State -->
                    <div
                        class="flex flex-col items-center justify-center h-full py-20"
                    >
                        <h1
                            class="text-5xl sm:text-6xl font-display font-black tracking-tightest text-base-content/20 text-center"
                        >
                            chat
                        </h1>
                        <p
                            class="text-sm font-mono uppercase tracking-widest text-base-content/40 mt-4"
                        >
                            Powered by Lionheart Intelligence
                        </p>
                    </div>
                {/if}

                {#if loading}
                    <div class="mb-8">
                        <div class="flex items-start gap-4">
                            <div class="flex-shrink-0">
                                <div
                                    class="w-8 h-8 rounded-full flex items-center justify-center text-base-content bg-base-content/10"
                                >
                                    <svg
                                        xmlns="http://www.w3.org/2000/svg"
                                        fill="none"
                                        viewBox="0 0 24 24"
                                        stroke-width="1.5"
                                        stroke="currentColor"
                                        class="w-5 h-5"
                                    >
                                        <path
                                            stroke-linecap="round"
                                            stroke-linejoin="round"
                                            d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z"
                                        />
                                    </svg>
                                </div>
                            </div>
                            <div class="flex-1">
                                <div
                                    class="text-xs font-bold uppercase tracking-widest text-base-content/50 mb-2"
                                >
                                    chat
                                </div>
                                <span
                                    class="loading loading-dots loading-md text-base-content/30"
                                ></span>
                            </div>
                        </div>
                    </div>
                {/if}
            </div>
        </div>

        <!-- Message Input -->
        <div class="border-t-2 border-base-content/10 bg-base-100 p-4">
            <div class="max-w-3xl mx-auto">
                <div
                    class="card bg-base-200 border-2 border-base-content/10 overflow-hidden"
                >
                    <textarea
                        bind:value={newMessage}
                        readonly={loading}
                        on:keydown={handleKeydown}
                        class="w-full py-4 px-4 bg-transparent focus:outline-none resize-none text-base-content placeholder:text-base-content/40"
                        placeholder={$currentChatConversation
                            ? "Continue the conversation..."
                            : "Ask chat about your training, wellness, or health data..."}
                        rows="3"
                    ></textarea>
                    <div
                        class="flex items-center justify-between px-4 py-3 border-t border-base-content/5"
                    >
                        <span
                            class="text-xs font-mono uppercase tracking-wider text-base-content/30"
                        >
                            GPT-5
                        </span>
                        <button
                            class="btn btn-primary btn-sm px-6 rounded-lg disabled:opacity-40"
                            on:click={sendMessage}
                            disabled={loading || !newMessage.trim()}
                            aria-label="Send message"
                        >
                            {#if loading}
                                <span class="loading loading-spinner loading-xs"
                                ></span>
                            {:else}
                                <svg
                                    xmlns="http://www.w3.org/2000/svg"
                                    fill="none"
                                    viewBox="0 0 24 24"
                                    stroke-width="2"
                                    stroke="currentColor"
                                    class="w-4 h-4"
                                >
                                    <path
                                        stroke-linecap="round"
                                        stroke-linejoin="round"
                                        d="M13.5 4.5L21 12m0 0l-7.5 7.5M21 12H3"
                                    />
                                </svg>
                            {/if}
                        </button>
                    </div>
                </div>
            </div>
        </div>
    </main>
</div>
