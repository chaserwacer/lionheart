<script lang="ts">
    import { onMount } from "svelte";
    import { fetchBootUserDto, bootUserDto } from "$lib/stores/stores";
    import {
        ChatEndpointClient,
        ChatRequest,
        CreateChatConversationRequest,
        CreateChatConversationEndpointClient,
        GetAllChatConversationsEndpointClient,
        GetChatConversationEndpointClient,
        ChatConversationDTO,
        ChatMessage,
        ChatMessageRole,
        DeleteChatConversationEndpointClient,
        DeleteChatConversationRequest,
    } from "$lib/api/ApiClient";
    import { goto } from "$app/navigation";
    import { page } from "$app/stores";
    import { get } from "svelte/store";

    const baseUrl =
        typeof window !== "undefined"
            ? window.location.origin
            : "http://localhost:5174";

    let newMessage = "";
    let loading = false;
    let chatContainer: HTMLElement;
    let currentChatConversation: ChatConversationDTO;
    let currentConversationId: string = "";
    // Chat conversations
    let conversations: ChatConversationDTO[] = [];
    let loadingConversations = true;
    let hoverConversationId: string | null = null;

    // Send a message to the AI
    async function sendMessage() {
        if (!newMessage.trim()) return;

        // Add user message
        const userQuery = newMessage.trim();
        newMessage = "";
        loading = true;

        // Optimistic UI: push a user message shaped like ChatMessageItemDTO
        currentChatConversation.messages.push({
            chatMessageItemID: "",
            chatConversationID: currentConversationId,
            chatMessageRole: ChatMessageRole._1,
            creationTime: new Date(),
            chatMessage: ChatMessage.fromJS({
                content: [{ text: userQuery }],
            }),
        } as any);
        // Scroll to bottom
        setTimeout(() => {
            if (chatContainer) {
                chatContainer.scrollTop = chatContainer.scrollHeight;
            }
        }, 50);

        try {
            // Call the chat API
            const chatClient = new ChatEndpointClient(baseUrl);
            var request = ChatRequest.fromJS({
                message: userQuery,
                chatConversationId: currentConversationId,
            });
            currentChatConversation = await chatClient.chatPOST(request);

            // Reset loading state
            loading = false;
        } catch (error) {
            console.error("Error:", error);
            loading = false;
        }
    }

    // Load all user conversations
    async function loadConversations() {
        try {
            loadingConversations = true;
            const client = new GetAllChatConversationsEndpointClient(baseUrl);
            const response = await client.getAll();
            conversations = response;
            loadingConversations = false;
        } catch (error) {
            console.error("Error loading conversations:", error);
            loadingConversations = false;
        }
    }

    // Select a conversation
    async function selectConversation(id: string) {
        if (id === currentConversationId) return;

        goto(`/chat/${id}`);
    }

    async function deleteConversation(id: string, ev?: MouseEvent) {
        if (ev) {
            ev.stopPropagation();
            ev.preventDefault();
        }
        const convo = conversations.find((c) => c.chatConversationId === id);
        const name = convo?.name || "this conversation";
        if (!confirm(`Delete '${name}'? This cannot be undone.`)) return;
        try {
            const client = new DeleteChatConversationEndpointClient(baseUrl);
            const req = DeleteChatConversationRequest.fromJS({
                chatConversationId: id,
            });
            await client.chatDELETE(req);
            await loadConversations();
            if (currentConversationId === id) {
                goto("/chat");
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

    onMount(async () => {
        await fetchBootUserDto(fetch);

        // Load conversations
        await loadConversations();
        currentConversationId = $page.params.chatId ?? "";
        if (currentConversationId) {
            try {
                const getChatConversationEndpointClient =
                    new GetChatConversationEndpointClient(baseUrl);
                currentChatConversation =
                    await getChatConversationEndpointClient.chatGET(
                        currentConversationId,
                    );
            } catch (error) {
                console.error("Error loading conversation:", error);
            }
        }

        console.log("chat page mounted, params:", $page.params.chatId);
    });

    function escapeHtml(str: string): string {
        return str
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function getSafeChatMessage(text: string | undefined) {
        if (!text) return "";
        return escapeHtml(text).replace(/\n/g, "<br>");
    }
</script>

<div class="flex flex-col bg-base w-full">
    <div class="flex h-full w-full">
        <!-- Sidebar -->
        <div class="hidden md:flex md:w-1/6 flex-col bg-base-300 border-r">
            <div class="p-4 flex justify-between items-center">
                <h2 class="text-lg font-medium text-base-content-300">
                    Chat History
                </h2>
                <button
                    class="btn btn-sm btn-ghost"
                    on:click={() => goto("/chat")}
                    aria-label="New chat"
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
                            d="M12 4.5v15m7.5-7.5h-15"
                        />
                    </svg>
                </button>
            </div>
            <div class="flex flex-1 overflow-y-scroll overflow-x-hidden">
                {#if loadingConversations}
                    <div class="p-4 text-center">
                        <span class="loading loading-spinner loading-md"></span>
                    </div>
                {:else if conversations.length === 0}
                    <div class="p-4 text-center text-sm text-base-content-300">
                        No conversations yet.
                    </div>
                {:else}
                    <div class="space-y-1 p-2 flex flex-col">
                        {#each conversations as conversation}
                            <button
                                class="group relative w-full text-left px-3 py-2 rounded-lg text-sm transition-colors flex flex-col
                                       {$page.params.chatId ===
                                conversation.chatConversationId
                                    ? 'bg-primary/10 text-primary'
                                    : 'hover:bg-base-200'}"
                                on:mouseenter={() =>
                                    (hoverConversationId =
                                        conversation.chatConversationId)}
                                on:mouseleave={() =>
                                    (hoverConversationId =
                                        hoverConversationId ===
                                        conversation.chatConversationId
                                            ? null
                                            : hoverConversationId)}
                                on:click={() =>
                                    selectConversation(
                                        conversation.chatConversationId,
                                    )}
                            >
                                <div
                                    class="flex items-center pr-6 justify-between"
                                >
                                    <svg
                                        xmlns="http://www.w3.org/2000/svg"
                                        fill="none"
                                        viewBox="0 0 24 24"
                                        stroke-width="1.5"
                                        stroke="currentColor"
                                        class="w-4 h-4 mr-2 flex-shrink-0"
                                    >
                                        <path
                                            stroke-linecap="round"
                                            stroke-linejoin="round"
                                            d="M7.5 8.25h9m-9 3H12m-9.75 1.51c0 1.6 1.123 2.994 2.707 3.227 1.129.166 2.27.293 3.423.379.35.026.67.21.865.501L12 21l2.755-4.133a1.14 1.14 0 01.865-.501 48.172 48.172 0 003.423-.379c1.584-.233 2.707-1.626 2.707-3.228V6.741c0-1.602-1.123-2.995-2.707-3.228A48.394 48.394 0 0012 3c-2.392 0-4.744.175-7.043.513C3.373 3.746 2.25 5.14 2.25 6.741v6.018z"
                                        />
                                    </svg>
                                    <div
                                        class=" w-3/4 overflow-hidden whitespace-nowrap"
                                    >
                                        {conversation.name}
                                    </div>
                                    <button
                                        type="button"
                                        class="flex text-xs text-base-content-300 hover:text-error cursor-pointer p-1 bg-transparent border-none
           opacity-0 group-hover:opacity-100 transition-opacity"
                                        on:click={(e) =>
                                            deleteConversation(
                                                conversation.chatConversationId,
                                                e,
                                            )}
                                    >
                                        ✕
                                    </button>
                                </div>
                                <div
                                    class="text-xs text-base-content-300 mt-1 truncate"
                                >
                                    {new Date(
                                        conversation.createdAt,
                                    ).toLocaleString()}
                                </div>
                            </button>
                        {/each}
                    </div>
                {/if}
            </div>
            <div class="p-4 border-t border-neutral">
                <div class="text-xs text-base-content-300">
                    Lionheart does not give medical advice, diagnosis, or
                    treatment. Lionheart AI can make mistakes.
                </div>
            </div>
        </div>

        <!-- Main Chat Area -->
        <div
            class="flex flex-col w-full md:w-5/6 items-center p-5 overflow-hidden"
        >
            <!-- Chat Messages -->
            <div
                bind:this={chatContainer}
                class="flex overflow-y-scroll px-4 w-full h-3/4"
            >
                <div class="flex flex flex-col max-w-5xl mx-auto w-full">
                    {#if currentChatConversation && currentChatConversation.messages}
                        {#each currentChatConversation.messages as message, idx}
                            {#if message.chatMessageRole !== ChatMessageRole._0 && message.chatMessageRole !== ChatMessageRole._3 && idx != 0}
                                <div class="mb-6 w-full">
                                    <div class="flex items-start w-full">
                                        <!-- Avatar -->
                                        <div class="mr-4 mt-1 flex">
                                            {#if message.chatMessageRole === ChatMessageRole._1}
                                                <div
                                                    class="w-8 h-8 bg-blue-600 rounded-full flex items-center justify-center text-white"
                                                ></div>
                                            {:else}
                                                <div
                                                    class="w-8 h-8 bg-neutral-600 dark:bg-neutral-500 rounded-full flex items-center justify-center text-white"
                                                >
                                                    <!-- icon -->
                                                </div>
                                            {/if}
                                        </div>

                                        <!-- Message Content -->
                                        <div class="flex flex-col w-full">
                                            <div
                                                class="text-sm font-medium text-primary mb-1 w-full"
                                            >
                                                {message.chatMessageRole ===
                                                ChatMessageRole._1
                                                    ? $bootUserDto?.name ||
                                                      "You"
                                                    : "HAL"}
                                            </div>
                                            <div
                                                class="bg-base-200 dark:bg-base-300 rounded-lg p-3 w-full break-words"
                                            >
                                                {@html getSafeChatMessage(
                                                    message.chatMessage
                                                        .content?.[0]?.text,
                                                )}
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            {/if}
                        {/each}
                    {/if}

                    {#if loading}
                        <!-- loading indicator -->
                    {/if}
                </div>
            </div>

            <!-- Message Input -->
            <div class="w-full p-5 pb-0 md:px-6">
                <div class="max-w-5xl mx-auto">
                    <div class="relative">
                        <div
                            class="rounded-lg shadow-sm bg-base-300 overflow-hidden"
                        >
                            <textarea
                                bind:value={newMessage}
                                readonly={loading}
                                on:keydown={handleKeydown}
                                class="w-full py-3 px-4 overflow-y-scroll focus:outline-none resize-none bg-transparent text"
                                placeholder="HAL can provide insights and analyses of your data."
                                rows="3"
                            ></textarea>
                            <div
                                class="flex items-center justify-between px-4 py-2"
                            >
                                <div
                                    class="flex items-center space-x-2 text-xs text-info"
                                >
                                    <div>powered by gpt 5</div>
                                </div>
                                <button
                                    class=" btn btn-primary m-0 btn-sm disabled:opacity-50 disabled:cursor-not-allowed"
                                    on:click={() => sendMessage()}
                                    disabled={loading || !newMessage.trim()}
                                    aria-label="Send message"
                                >
                                    {#if loading}
                                        <svg
                                            class="animate-spin h-5 w-5"
                                            xmlns="http://www.w3.org/2000/svg"
                                            fill="none"
                                            viewBox="0 0 24 24"
                                        >
                                            <circle
                                                class="opacity-25"
                                                cx="12"
                                                cy="12"
                                                r="10"
                                                stroke="currentColor"
                                                stroke-width="4"
                                            ></circle>
                                            <path
                                                class="opacity-75"
                                                fill="currentColor"
                                                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                                            ></path>
                                        </svg>
                                    {:else}
                                        ->
                                    {/if}
                                </button>
                            </div>
                        </div>
                        <div
                            class="mt-2 text-xs text-center text-neutral"
                        ></div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</div>
