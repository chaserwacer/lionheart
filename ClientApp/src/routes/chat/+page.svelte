<script lang="ts">
    import { onMount } from "svelte";
    import { fetchBootUserDto, bootUserDto } from "$lib/stores/stores";
    import { ChatEndpointClient, ChatRequest } from "$lib/api/ApiClient";

    const baseUrl =
        typeof window !== "undefined"
            ? window.location.origin
            : "http://localhost:5174";
    // Chat state
    let messages = [
        {
            role: "assistant",
            content:
                "Hello! I'm your Lionheart training assistant. I can help you track your workouts, check your progress, and provide insights based on your training data. How can I assist you today?",
        },
    ];
    let newMessage = "";
    let loading = false;
    let chatContainer: HTMLElement;
    let creativityLevel = 3; // Default creativity level (1-5)

    // Mock data for development - replace with actual API calls
    async function sendMessage() {
        if (!newMessage.trim()) return;

        // Add user message
        messages = [...messages, { role: "user", content: newMessage.trim() }];
        const userQuery = newMessage.trim();
        newMessage = "";
        loading = true;

        // Scroll to bottom
        setTimeout(() => {
            if (chatContainer) {
                chatContainer.scrollTop = chatContainer.scrollHeight;
            }
        }, 50);

        try {
            // Replace with your actual API endpoint
            const chatClient = new ChatEndpointClient(baseUrl);
            var request = ChatRequest.fromJS({
                message: userQuery,
                creativityLevel: creativityLevel,
            });
            var response = await chatClient.chat(request);

            if (!response || !response.response) {
                throw new Error("Failed to get response");
            }

            // Add assistant response
            messages = [
                ...messages,
                { role: "assistant", content: response.response },
            ];
            loading = false;

            // Scroll to bottom again after response
            setTimeout(() => {
                if (chatContainer) {
                    chatContainer.scrollTop = chatContainer.scrollHeight;
                }
            }, 50);
        } catch (error) {
            console.error("Error:", error);
            messages = [
                ...messages,
                {
                    role: "assistant",
                    content:
                        "I'm sorry, I encountered an error while processing your request. Please try again later.",
                },
            ];
            loading = false;
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
        if (chatContainer) {
            chatContainer.scrollTop = chatContainer.scrollHeight;
        }
    });
</script>

<div class="flex flex-col bg-base w-full">
    <div class="flex h-full mb-100 pb-100">
        <!-- Sidebar -->
        <div
            class="hidden md:flex md:w-64 flex-col bg-base-300 border-r "
        >
            <div class="p-4">
                <h2
                    class="text-lg font-medium text-base-content-300"
                >
                    LH Training Intelligence
                </h2>
            </div>
            <div class="flex-1 overflow-y-auto">
                <!-- History would go here -->
            </div>
            <div
                class="p-4 border-t border-neutral"
            >
                <div class="text-xs text-base-content-300">
                    Lionheart does not give medical advice, diagnosis, or
                    treatment. Lionheart AI can make mistakes.
                </div>
            </div>
        </div>

        <!-- Main Chat Area -->
        <div class="flex-1 flex flex-col overflow-hidden">
            <!-- Chat Messages -->
            <div bind:this={chatContainer} class="flex-1 overflow-y-auto">
                <div class="max-w-3xl mx-auto px-4 py-6">
                    {#each messages as message}
                        <div class="mb-6">
                            <div class="flex items-start">
                                <!-- Avatar -->
                                <div class="mr-4 mt-1">
                                    {#if message.role === "user"}
                                        <div
                                            class="w-8 h-8 bg-blue-600 rounded-full flex items-center justify-center text-white"
                                        >
                                            <span class="text-sm font-medium"
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
                                <div class="flex-1">
                                    <div
                                        class="text-sm font-medium text-primary mb-1"
                                    >
                                        {message.role === "user"
                                            ? $bootUserDto?.name || "You"
                                            : "HAL"}
                                    </div>
                                    <div
                                        class="prose prose-neutral prose-sm text-base-content dark:prose-invert max-w-none"
                                    >
                                        {message.content}
                                    </div>
                                </div>
                            </div>
                        </div>
                    {/each}

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
                                <div class="flex-1">
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
            <div
                class="border-t border py-4 px-4 md:px-6"
            >
                <div class="max-w-3xl mx-auto">
                    <div class="relative">
                        <div
                            class="rounded-lg border  shadow-sm bg-base-300 overflow-hidden"
                        >
                            <textarea
                                bind:value={newMessage}
                                on:keydown={handleKeydown}
                                class="w-full py-3 px-4 overflow-y-scroll focus:outline-none resize-none bg-transparent text"
                                placeholder="Message HAL..."
                                rows="3"
                            ></textarea>
                            <div
                                class="flex items-center justify-between px-4 py-2 border-t border"
                            >
                                <div
                                    class="flex items-center space-x-2 text-xs text-neutral"
                                >
                                    <div>Creativity:</div>

                                    <div
                                        class=" ml-4 text-xs text-neutral"
                                    >
                                        <input
                                            type="range"
                                            class=" bg-transparent range range-primary border-none focus:outline-none text-center"
                                            required
                                            title="Creativity Level"
                                            bind:value={creativityLevel}
                                            min="1"
                                            max="5"
                                        />
                                    </div>
                                </div>
                                <button
                                    class=" btn btn-primary m-0 btn-sm disabled:opacity-50 disabled:cursor-not-allowed"
                                    on:click={sendMessage}
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
