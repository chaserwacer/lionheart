<script lang="ts">
    import { createEventDispatcher } from 'svelte';

    export let disabled: boolean = false;
    export let placeholder: string = 'Type a message...';

    let message = '';
    let textareaElement: HTMLTextAreaElement;

    const dispatch = createEventDispatcher<{ send: string }>();

    function handleSubmit() {
        const trimmed = message.trim();
        if (trimmed && !disabled) {
            dispatch('send', trimmed);
            message = '';
            resetTextareaHeight();
        }
    }

    function handleKeydown(e: KeyboardEvent) {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            handleSubmit();
        }
    }

    function handleInput() {
        adjustTextareaHeight();
    }

    function adjustTextareaHeight() {
        if (textareaElement) {
            textareaElement.style.height = 'auto';
            const newHeight = Math.min(textareaElement.scrollHeight, 200);
            textareaElement.style.height = `${newHeight}px`;
        }
    }

    function resetTextareaHeight() {
        if (textareaElement) {
            textareaElement.style.height = 'auto';
        }
    }
</script>

<form on:submit|preventDefault={handleSubmit} class="relative">
    <div class="flex items-end gap-2 p-3 bg-base-100 border-t border-base-content/10">
        <div class="flex-1 relative">
            <textarea
                bind:this={textareaElement}
                bind:value={message}
                on:keydown={handleKeydown}
                on:input={handleInput}
                {placeholder}
                {disabled}
                rows="1"
                class="w-full resize-none bg-base-200 rounded-xl px-4 py-2.5 pr-12
                       text-sm leading-relaxed placeholder:text-base-content/40
                       border border-transparent
                       focus:border-base-content/20 focus:outline-none focus:bg-base-100
                       disabled:opacity-50 disabled:cursor-not-allowed
                       transition-colors duration-150"
                style="min-height: 42px; max-height: 200px;"
            ></textarea>

            <!-- Character hint -->
            {#if message.length > 0}
                <span class="absolute bottom-2 right-12 text-xs text-base-content/30 tabular-nums">
                    {message.length}
                </span>
            {/if}
        </div>

        <button
            type="submit"
            disabled={disabled || !message.trim()}
            class="btn btn-sm btn-square bg-base-content text-base-100 border-0
                   hover:bg-base-content/90
                   disabled:bg-base-content/20 disabled:text-base-content/40
                   transition-colors duration-150 flex-shrink-0"
        >
            {#if disabled}
                <span class="loading loading-spinner loading-xs"></span>
            {:else}
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-4 h-4">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M6 12 3.269 3.125A59.769 59.769 0 0 1 21.485 12 59.768 59.768 0 0 1 3.27 20.875L5.999 12Zm0 0h7.5" />
                </svg>
            {/if}
        </button>
    </div>
</form>
