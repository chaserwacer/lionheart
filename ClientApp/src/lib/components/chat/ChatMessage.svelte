<script lang="ts">
    import type { ILHChatMessageDTO } from '$lib/api/ApiClient';

    export let message: ILHChatMessageDTO;
    export let isUser: boolean = false;
    export let isLoading: boolean = false;

    function formatTime(date: Date | undefined): string {
        if (!date) return '';
        const d = new Date(date);
        return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }

    // Simple markdown-like formatting for code blocks and line breaks
    function formatContent(content: string | undefined): string {
        if (!content) return '';

        // Escape HTML
        let formatted = content
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');

        // Code blocks (```code```)
        formatted = formatted.replace(/```(\w+)?\n?([\s\S]*?)```/g, (_, lang, code) => {
            return `<pre class="bg-base-300 rounded-lg p-3 my-2 overflow-x-auto text-sm font-mono"><code>${code.trim()}</code></pre>`;
        });

        // Inline code (`code`)
        formatted = formatted.replace(/`([^`]+)`/g, '<code class="bg-base-300 px-1.5 py-0.5 rounded text-sm font-mono">$1</code>');

        // Bold (**text**)
        formatted = formatted.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');

        // Italic (*text*)
        formatted = formatted.replace(/\*([^*]+)\*/g, '<em>$1</em>');

        // Line breaks
        formatted = formatted.replace(/\n/g, '<br>');

        return formatted;
    }
</script>

<div class="flex {isUser ? 'justify-end' : 'justify-start'} mb-4">
    <div class="flex items-end gap-2 max-w-[85%] {isUser ? 'flex-row-reverse' : ''}">
        <!-- Avatar -->
        <div class="flex-shrink-0 w-7 h-7 rounded-lg flex items-center justify-center text-xs
                    {isUser ? 'bg-base-content text-base-100' : 'bg-base-200 text-base-content border border-base-content/10'}">
            {#if isUser}
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-3.5 h-3.5">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0ZM4.501 20.118a7.5 7.5 0 0 1 14.998 0A17.933 17.933 0 0 1 12 21.75c-2.676 0-5.216-.584-7.499-1.632Z" />
                </svg>
            {:else}
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-3.5 h-3.5">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M9.813 15.904 9 18.75l-.813-2.846a4.5 4.5 0 0 0-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 0 0 3.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 0 0 3.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 0 0-3.09 3.09ZM18.259 8.715 18 9.75l-.259-1.035a3.375 3.375 0 0 0-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 0 0 2.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 0 0 2.456 2.456L21.75 6l-1.035.259a3.375 3.375 0 0 0-2.456 2.456ZM16.894 20.567 16.5 21.75l-.394-1.183a2.25 2.25 0 0 0-1.423-1.423L13.5 18.75l1.183-.394a2.25 2.25 0 0 0 1.423-1.423l.394-1.183.394 1.183a2.25 2.25 0 0 0 1.423 1.423l1.183.394-1.183.394a2.25 2.25 0 0 0-1.423 1.423Z" />
                </svg>
            {/if}
        </div>

        <!-- Message Bubble -->
        <div class="flex flex-col {isUser ? 'items-end' : 'items-start'}">
            <div class="rounded-xl px-4 py-2.5
                        {isUser
                            ? 'bg-base-content text-base-100 rounded-br-sm'
                            : 'bg-base-100 border border-base-content/10 rounded-bl-sm'}">
                {#if isLoading}
                    <div class="flex items-center gap-1 py-1">
                        <span class="w-2 h-2 bg-current rounded-full animate-bounce" style="animation-delay: 0ms;"></span>
                        <span class="w-2 h-2 bg-current rounded-full animate-bounce" style="animation-delay: 150ms;"></span>
                        <span class="w-2 h-2 bg-current rounded-full animate-bounce" style="animation-delay: 300ms;"></span>
                    </div>
                {:else}
                    <div class="text-sm leading-relaxed prose prose-sm max-w-none
                                {isUser ? 'prose-invert' : ''}">
                        {@html formatContent(message.content)}
                    </div>
                {/if}
            </div>

            <!-- Timestamp -->
            {#if message.creationTime && !isLoading}
                <span class="text-xs text-base-content/40 mt-1 font-mono {isUser ? 'mr-1' : 'ml-1'}">
                    {formatTime(message.creationTime)}
                </span>
            {/if}
        </div>
    </div>
</div>
