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
        DeleteChatConversationEndpointClient,
        DeleteChatConversationRequest,
        ChatMessageRole,
    } from "$lib/api/ApiClient";
    import { goto } from "$app/navigation";
    import { page } from "$app/stores";
    import { slugify } from "$lib/utils/slugify";
    import { load } from "../+layout";

    const baseUrl =
        typeof window !== "undefined"
            ? window.location.origin
            : "http://localhost:5174";

    let newMessage = "";
    let loading = false;
    let chatContainer: HTMLElement;

    // Chat conversations
    let conversations: ChatConversationDTO[] = [];
    let loadingConversations = true;
    let chatConversation: ChatConversationDTO;
    let hoverConversationId: string | null = null; // track hover to show delete X

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

    // Create a new conversation
    async function createNewConversation(initialMessage: string) {
        try {
            loading = true;
            const createRequest = new CreateChatConversationRequest();
            createRequest.name =
                initialMessage.length > 30
                    ? initialMessage.substring(0, 27) + "..."
                    : initialMessage; // Use first message as conversation name

            const createClient = new CreateChatConversationEndpointClient(
                baseUrl,
            );
            const chatClient = new ChatEndpointClient(baseUrl);
            chatConversation = await createClient.chatPOST2(createRequest);
            // chatConversation.messages = [
            //     {
            //         chatMessageRole: ChatMessageRole._1,
            //         content: initialMessage,
            //         chatMessageItemID: "",
            //         chatConversationID: "",
            //         creationTime: new Date(),
            //         init: function (_data?: any): void {
            //             throw new Error("Function not implemented.");
            //         },
            //         toJSON: function (data?: any) {
            //             throw new Error("Function not implemented.");
            //         },
            //     },
            // ];
            var request = ChatRequest.fromJS({
                message: initialMessage,
                chatConversationId: chatConversation.chatConversationId,
            });
            await chatClient.chatPOST(request);

            // Update the URL with the new conversation ID
            await goto(
                `/chat/${slugify(chatConversation.chatConversationId)}`,
                {
                    replaceState: false,
                },
            );
        } catch (error) {
            console.error("Error creating new conversation:", error);
            return null;
        }
    }

    // Select a conversation
    function selectConversation(id: string) {
        goto(`/chat/${id}`);
    }

    // Delete a conversation
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
            // Reload conversations
            await loadConversations();
            // If we deleted the current one navigate away to blank chat page
            if ($page.params.chatId === id) {
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
            createNewConversation(newMessage);
        }
    }

    onMount(async () => {
        await fetchBootUserDto(fetch);

        // Load conversations
        await loadConversations();
    });
    let currentConversationId = $page.params.chatId ?? "";
    $: if ($page.params.chatId) {
        (async () => {
            currentConversationId = $page.params.chatId ?? "";
            if (currentConversationId) {
                try {
                    const getChatConversationEndpointClient =
                        new GetChatConversationEndpointClient(baseUrl);
                    chatConversation =
                        await getChatConversationEndpointClient.chatGET(
                            currentConversationId,
                        );
                } catch (error) {
                    console.error("Error loading conversation:", error);
                }
            }
        })();
    }
</script>

<div class="flex flex-col bg-base w-full">
    <div class="flex h-full">
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
        <div class="flex mx-auto w-full flex-col overflow-hidden">
            <!-- Chat Messages -->
            <div
                bind:this={chatContainer}
                class="flex overflow-y-scroll px-4 w-full h-3/4"
            >
                <div class="flex flex flex-col max-w-3xl mx-auto w-full">
                    {#if chatConversation && chatConversation.messages}
                        {#each chatConversation.messages as message, idx}
                            {#if message.chatMessage.content}
                                <div class="mb-6">
                                    <div class="flex items-start w-full">
                                        <!-- Avatar -->
                                        <div class="mr-4 mt-1">
                                            {#if message.chatMessageRole !== ChatMessageRole._0 && message.chatMessageRole !== ChatMessageRole._3 && idx != 0}
                                                <div
                                                    class="w-8 h-8 bg-blue-600 rounded-full flex items-center justify-center text-white"
                                                >
                                                    <span
                                                        class="text-sm font-medium"
                                                        >Y</span
                                                    >
                                                </div>
                                            {:else}
                                                <div
                                                    class="w-8 h-8 bg-neutral-600 dark:bg-neutral-500 rounded-full flex items-center justify-center text-white"
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
                                        <div class="flex">
                                            <div
                                                class="text-sm font-medium text-primary mb-1"
                                            >
                                                {message.chatMessageRole ===
                                                ChatMessageRole._1
                                                    ? $bootUserDto?.name ||
                                                      "You"
                                                    : "HAL"}
                                            </div>
                                            <div
                                                class="prose prose-neutral prose-sm text-base-content dark:prose-invert max-w-none"
                                            >
                                                {message.chatMessage.content?.[0]?.text || ''}
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            {/if}
                        {/each}
                    {:else}
                        <p class="text-6xl font-thin text-center pt-20">
                            Let's get started.
                        </p>
                        <p class="text-sm font-bold text-center pt-10">
                            HAL, is powered by Lionheart Intelligence.
                        </p>
                    {/if}
                    {#if loading}
                        <div class="mb-6">
                            <div class="flex items-start">
                                <div class="mr-4 mt-1">
                                    <div
                                        class="w-8 h-8 bg-neutral-600 dark:bg-neutral-500 rounded-full flex items-center justify-center text-white"
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
                                <div class="flex flex-col">
                                    <div
                                        class="text-sm font-medium text-neutral mb-1"
                                    >
                                        HAL
                                    </div>
                                    <div class="flex">
                                        <span
                                            class="loading loading-dots loading-md"
                                        ></span>
                                    </div>
                                </div>
                            </div>
                        </div>
                    {/if}
                </div>
            </div>

            <!-- Message Input -->
            <div class=" py-4 px-4 md:px-6">
                <div class="max-w-3xl mx-auto">
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
                                    on:click={() =>
                                        createNewConversation(
                                            newMessage.trim(),
                                        )}
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
