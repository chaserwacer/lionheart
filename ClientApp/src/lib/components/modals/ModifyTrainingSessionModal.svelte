<script lang="ts">
  import { createEventDispatcher } from 'svelte';
  import type { TrainingSessionDTO } from '$lib/api/ApiClient';

  export let show = false;
  export let session: TrainingSessionDTO;

  const dispatch = createEventDispatcher();

  let isSubmitting = false;
  let userPrompt = '';
  let aiResponse = '';
  let errorMsg = '';

  // Derive session info for display/context
  $: sessionId = (session as any)?.trainingSessionID ?? '';
  $: movementCount = (session as any)?.movements?.length ?? 0;

  const suggestionPrompts = [
    'Make this session easier - I\'m feeling fatigued',
    'Increase the intensity for today',
    'Add a warmup routine',
    'Suggest alternative exercises for my equipment',
    'Reduce the volume but maintain intensity',
  ];

  function handleClose() {
    show = false;
    userPrompt = '';
    aiResponse = '';
    errorMsg = '';
    dispatch('close');
  }

  function handleBackdropClick(e: MouseEvent) {
    if (e.target === e.currentTarget) {
      handleClose();
    }
  }

  function selectSuggestion(prompt: string) {
    userPrompt = prompt;
  }

  async function handleSubmit() {
    if (!userPrompt.trim()) return;

    isSubmitting = true;
    errorMsg = '';
    aiResponse = '';

    try {
      // TODO: Implement actual AI endpoint call
      // For now, show a placeholder response
      await new Promise((resolve) => setTimeout(resolve, 1500));
      
      aiResponse = `AI modification feature coming soon! Your request: "${userPrompt}" will be processed to modify your training session with ${movementCount} movements.`;
      
      // In a real implementation, you would:
      // 1. Call an AI endpoint with the session data and user prompt
      // 2. Receive modified session/movement data
      // 3. Apply changes and refresh the session
      
    } catch (e: any) {
      errorMsg = e?.message || 'Failed to process AI request';
    } finally {
      isSubmitting = false;
    }
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape') {
      handleClose();
    }
  }
</script>

<svelte:window on:keydown={handleKeydown} />

{#if show}
  <!-- svelte-ignore a11y-click-events-have-key-events -->
  <!-- svelte-ignore a11y-no-noninteractive-element-interactions -->
  <!-- Modal backdrop -->
  <div
    class="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4"
    on:click={handleBackdropClick}
    role="dialog"
    aria-modal="true"
    aria-labelledby="ai-modal-title"
  >
    <!-- Modal content -->
    <div class="bg-base-100 rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-hidden flex flex-col">
      <!-- Header -->
      <div class="p-6 border-b border-base-content/10">
        <div class="flex items-center justify-between">
          <h2 id="ai-modal-title" class="text-2xl font-display font-black">Modify with AI</h2>
          <button
            class="btn btn-sm btn-ghost btn-circle"
            on:click={handleClose}
            aria-label="Close"
          >
            ✕
          </button>
        </div>
        <p class="text-sm text-base-content/60 mt-1">
          Tell the AI how you'd like to modify this training session
        </p>
      </div>

      <!-- Body -->
      <div class="p-6 flex-1 overflow-y-auto">
        <!-- Quick suggestions -->
        <div class="mb-4">
          <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-2">
            Quick suggestions
          </div>
          <div class="flex flex-wrap gap-2">
            {#each suggestionPrompts as prompt}
              <button
                class="btn btn-xs btn-outline rounded-full"
                on:click={() => selectSuggestion(prompt)}
                disabled={isSubmitting}
              >
                {prompt}
              </button>
            {/each}
          </div>
        </div>

        <!-- User input -->
        <div class="mb-4">
          <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-2">
            Your request
          </div>
          <textarea
            class="textarea textarea-bordered w-full text-base"
            rows="4"
            bind:value={userPrompt}
            placeholder="Describe how you'd like to modify this session..."
            disabled={isSubmitting}
          />
        </div>

        <!-- Error message -->
        {#if errorMsg}
          <div class="alert alert-error rounded-xl mb-4">
            <span>{errorMsg}</span>
          </div>
        {/if}

        <!-- AI Response -->
        {#if aiResponse}
          <div class="mb-4">
            <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-2">
              AI Response
            </div>
            <div class="p-4 bg-base-200 rounded-xl text-base whitespace-pre-wrap">
              {aiResponse}
            </div>
          </div>
        {/if}
      </div>

      <!-- Footer -->
      <div class="p-6 border-t border-base-content/10 flex justify-end gap-3">
        <button class="btn btn-ghost rounded-xl" on:click={handleClose} disabled={isSubmitting}>
          Cancel
        </button>
        <button
          class="btn btn-primary rounded-xl"
          on:click={handleSubmit}
          disabled={isSubmitting || !userPrompt.trim()}
        >
          {#if isSubmitting}
            <span class="loading loading-spinner loading-sm"></span>
            Processing...
          {:else}
            Apply Modifications
          {/if}
        </button>
      </div>
    </div>
  </div>
{/if}
