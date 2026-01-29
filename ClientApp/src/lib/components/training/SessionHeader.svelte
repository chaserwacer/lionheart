<script lang="ts">
  import { goto } from '$app/navigation';
  import { WeightUnit, type TrainingSessionDTO } from '$lib/api/ApiClient';
  import {
    session,
    isEditing,
    isLoading,
    displayWeightUnit,
    enterEditMode,
    cancelEditMode,
    saveEdits,
    deleteSession,
  } from '$lib/stores/sessionStore';
  import { sessionStatusValue, sessionDateUS } from '$lib/utils/training';
  import { statusLabel } from '$lib/utils/training';

  export let programId: string = '';
  export let sessionId: string;

  function goBack() {
    goto('/training');
  }

  function handleEnterEdit() {
    enterEditMode();
  }

  async function handleSaveEdits() {
    const success = await saveEdits(sessionId, programId || undefined);
    if (success) {
      location.reload();
    }
  }

  function handleCancelEdit() {
    cancelEditMode(sessionId, programId || undefined);
  }

  async function handleDeleteSession() {
    if (!confirm('Are you sure you want to delete this session? This cannot be undone.')) return;
    const success = await deleteSession(sessionId);
    if (success) {
      goto(programId ? `/training/program/${programId}` : '/training');
    }
  }
</script>

<header class="mb-8">
  <button
    on:click={goBack}
    class="group flex items-center gap-2 text-base-content/50 hover:text-primary transition-colors mb-6"
  >
    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 group-hover:-translate-x-1 transition-transform" fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
    </svg>
    <span class="text-sm font-medium">Back to Training</span>
  </button>

  <div class="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-6">
    <div>
      <h1
        class="text-4xl sm:text-5xl font-display font-black tracking-tight text-base-content leading-none"
      >
        Training Session
      </h1>

      {#if $session}
        <div class="mt-3 flex flex-wrap items-center gap-2">
          <span class="badge badge-lg badge-primary/20 text-primary font-semibold">
            {statusLabel(sessionStatusValue($session))}
          </span>
          <span class="text-sm sm:text-base font-medium text-base-content/70">
            {sessionDateUS($session)}
          </span>
        </div>
      {/if}
    </div>

    <div class="flex flex-wrap items-center gap-2">
      {#if !$isEditing}
        <button
          class="btn btn-ghost btn-sm gap-2 hover:bg-base-content/10"
          disabled={!$session}
          on:click={handleEnterEdit}
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
          </svg>
          Edit
        </button>
      {:else}
        <button
          class="btn btn-primary btn-sm"
          on:click={handleSaveEdits}
          disabled={$isLoading}
        >
          Done
        </button>
        <button
          class="btn btn-ghost btn-sm"
          on:click={handleCancelEdit}
          disabled={$isLoading}
        >
          Cancel
        </button>
        <div class="divider divider-horizontal mx-0 h-6"></div>
        <button
          class="btn btn-ghost btn-sm text-error hover:bg-error/10"
          on:click={handleDeleteSession}
          disabled={$isLoading}
        >
          Delete
        </button>
      {/if}
    </div>
  </div>
</header>
