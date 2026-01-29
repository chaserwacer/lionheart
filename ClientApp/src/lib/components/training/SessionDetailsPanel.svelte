<script lang="ts">
  import { TrainingSessionStatus } from '$lib/api/ApiClient';
  import {
    session,
    isEditing,
    draftDate,
    draftStatus,
    draftNotes,
    SESSION_STATUSES,
  } from '$lib/stores/sessionStore';
  import { sessionNotesValue, hasSessionNotes } from '$lib/utils/training';
  import { statusLabel } from '$lib/utils/training';
</script>

<div class="mb-6">
  {#if !$isEditing}
    {#if hasSessionNotes($session)}
      <div class="card bg-base-100/60 backdrop-blur border border-base-content/5 rounded-xl">
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
      </div>
    {/if}
  {:else}
    <div class="card bg-base-100 border border-base-content/10 rounded-xl">
      <div class="card-body p-4">
        <div class="flex flex-wrap items-center gap-3">
          <div>
            <span class="block text-xs font-medium text-base-content/50 mb-1">Date</span>
            <input
              class="input input-sm input-bordered rounded-lg"
              type="date"
              bind:value={$draftDate}
            />
          </div>
          <div>
            <span class="block text-xs font-medium text-base-content/50 mb-1">Status</span>
            <select class="select select-sm select-bordered rounded-lg" bind:value={$draftStatus}>
              {#each SESSION_STATUSES as s}
                <option value={s}>{statusLabel(s)}</option>
              {/each}
            </select>
          </div>
        </div>

        <div class="mt-4">
          <span class="block text-xs font-medium text-base-content/50 mb-1">Notes</span>
          <textarea
            class="textarea textarea-bordered w-full text-base rounded-lg"
            rows="3"
            bind:value={$draftNotes}
            placeholder="Add notes for this session..."
          />
        </div>
      </div>
    </div>
  {/if}
</div>
