<script lang="ts">
  import { page } from '$app/stores';
  import {
    session,
    draftNotes,
    updateSessionField,
  } from '$lib/stores/sessionStore';
  import { sessionNotesValue, hasSessionNotes } from '$lib/utils/training';

  let editingNotes = false;
  let sessionId = '';
  $: ({ sessionId } = $page.params as any);

  function startEditNotes() {
    draftNotes.set(sessionNotesValue($session) || '');
    editingNotes = true;
  }

  async function saveNotes() {
    await updateSessionField(sessionId, 'notes', $draftNotes);
    editingNotes = false;
  }

  function cancelNotesEdit() {
    editingNotes = false;
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape') {
      cancelNotesEdit();
    }
  }
</script>

<div class="mb-6">
  {#if editingNotes}
    <div class="card bg-base-100/60 backdrop-blur border border-primary/30 rounded-xl">
      <div class="card-body py-4 px-5">
        <div class="flex items-center gap-2 mb-2">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-base-content/40" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
          </svg>
          <span class="text-xs font-medium uppercase tracking-wider text-base-content/40">Notes</span>
        </div>
        <textarea
          class="textarea textarea-bordered w-full text-base rounded-lg bg-base-100"
          rows="3"
          bind:value={$draftNotes}
          placeholder="Add notes for this session..."
          on:keydown={handleKeydown}
        />
        <div class="flex justify-end gap-2 mt-2">
          <button class="btn btn-ghost btn-xs" on:click={cancelNotesEdit}>
            Cancel
          </button>
          <button class="btn btn-primary btn-xs" on:click={saveNotes}>
            Save
          </button>
        </div>
      </div>
    </div>
  {:else if hasSessionNotes($session)}
    <button
      class="card bg-base-100/60 backdrop-blur border border-base-content/5 rounded-xl w-full text-left hover:border-primary/30 transition-colors cursor-pointer"
      on:click={startEditNotes}
      title="Click to edit notes"
    >
      <div class="card-body py-4 px-5">
        <div class="flex items-center gap-2 mb-2">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-base-content/40" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
          </svg>
          <span class="text-xs font-medium uppercase tracking-wider text-base-content/40">Notes</span>
        </div>
        <p class="text-base text-base-content/80 whitespace-pre-wrap leading-relaxed">
          {sessionNotesValue($session)}
        </p>
      </div>
    </button>
  {:else}
    <button
      class="card bg-base-100/30 backdrop-blur border border-dashed border-base-content/10 rounded-xl w-full text-left hover:border-primary/30 hover:bg-base-100/50 transition-colors cursor-pointer"
      on:click={startEditNotes}
      title="Click to add notes"
    >
      <div class="card-body py-4 px-5">
        <div class="flex items-center gap-2">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-base-content/30" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          <span class="text-sm text-base-content/40">Add notes...</span>
        </div>
      </div>
    </button>
  {/if}
</div>
