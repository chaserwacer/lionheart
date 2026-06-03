<script lang="ts">
  import type { DraftSession } from '$lib/stores/ingestionStore';
  import { updateDraftMovement, removeDraftMovement, updateDraftSession, removeDraftSession } from '$lib/stores/ingestionStore';
  import DraftMovementRow from './DraftMovementRow.svelte';

  export let session: DraftSession;
  export let sessionIdx: number;

  $: dateDisplay = session.date ? new Date(session.date).toLocaleDateString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }) : 'No date';

  let editingDate = false;
  let dateValue = session.date ? session.date.split('T')[0] : '';

  function saveDate() {
    updateDraftSession(sessionIdx, { date: dateValue + 'T00:00:00' });
    editingDate = false;
  }
</script>

<div class="card bg-base-100 border border-base-content/10 shadow-sm">
  <div class="card-body p-4">
    <!-- Session header -->
    <div class="flex items-center justify-between mb-3">
      <div class="flex items-center gap-3">
        <h3 class="font-semibold text-lg">Session {sessionIdx + 1}</h3>
        {#if editingDate}
          <input
            type="date"
            class="input input-sm input-bordered rounded-lg"
            bind:value={dateValue}
            on:blur={saveDate}
            on:keydown={(e) => e.key === 'Enter' && saveDate()}
          />
        {:else}
          <button
            class="text-sm text-base-content/60 hover:text-primary cursor-pointer"
            on:click={() => { editingDate = true; }}
          >
            {dateDisplay}
          </button>
        {/if}
      </div>
      <div class="flex items-center gap-2">
        <span class="text-xs text-base-content/40">
          {session.movements.length} movement{session.movements.length !== 1 ? 's' : ''}
        </span>
        <button
          class="btn btn-ghost btn-xs text-error"
          on:click={() => removeDraftSession(sessionIdx)}
        >
          Remove
        </button>
      </div>
    </div>

    <!-- Notes -->
    {#if session.notes}
      <p class="text-sm text-base-content/50 mb-3 italic">{session.notes}</p>
    {/if}

    <!-- Movements -->
    <div class="space-y-2">
      {#each session.movements as movement, movIdx (movement.draftId)}
        <DraftMovementRow
          {movement}
          sessionIdx={sessionIdx}
          movementIdx={movIdx}
          onUpdate={(patch) => updateDraftMovement(sessionIdx, movIdx, patch)}
          onRemove={() => removeDraftMovement(sessionIdx, movIdx)}
        />
      {/each}
    </div>

    {#if session.movements.length === 0}
      <p class="text-sm text-base-content/40 text-center py-4">No movements parsed for this session.</p>
    {/if}
  </div>
</div>
