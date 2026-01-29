<script lang="ts">
  import { goto } from "$app/navigation";
  import {
    session,
    isEditing,
    isLoading,
    draftDate,
    draftStatus,
    enterEditMode,
    cancelEditMode,
    saveEdits,
    deleteSession,
    updateSessionField,
    SESSION_STATUSES,
  } from "$lib/stores/sessionStore";
  import {
    sessionStatusValue,
    sessionDateUS,
    toIsoDateOnly,
  } from "$lib/utils/training";
  import { statusLabel } from "$lib/utils/training";

  export let programId: string = "";
  export let sessionId: string;

  let editingDate = false;
  let editingStatus = false;

  function goBack() {
    goto("/training");
  }

  function handleEnterEdit() {
    enterEditMode();
  }

  async function handleSaveEdits() {
    await saveEdits(sessionId);
  }

  function handleCancelEdit() {
    cancelEditMode(sessionId, programId || undefined);
  }

  async function handleDeleteSession() {
    if (
      !confirm(
        "Are you sure you want to delete this session? This cannot be undone.",
      )
    )
      return;
    const success = await deleteSession(sessionId);
    if (success) {
      goto(programId ? `/training/program/${programId}` : "/training");
    }
  }

  function startEditDate() {
    if ($session) {
      draftDate.set(toIsoDateOnly(($session as any).date));
      editingDate = true;
    }
  }

  async function saveDate() {
    if (!$draftDate) return;
    await updateSessionField(
      sessionId,
      "date",
      $draftDate,
      programId || undefined,
    );
    editingDate = false;
  }

  function cancelDateEdit() {
    editingDate = false;
  }

  function startEditStatus() {
    if ($session) {
      draftStatus.set(($session as any).status);
      editingStatus = true;
    }
  }

  async function saveStatus() {
    await updateSessionField(
      sessionId,
      "status",
      $draftStatus,
      programId || undefined,
    );
    editingStatus = false;
  }

  function cancelStatusEdit() {
    editingStatus = false;
  }
</script>

<header class="mb-8">
  <div
    class="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-6"
  >
    <div>
      <h1
        class="text-4xl sm:text-5xl font-display font-black tracking-tight text-base-content leading-none"
      >
        Training Session
      </h1>

      {#if $session}
        <div class="mt-3 flex flex-col items-start gap-2">
          <!-- Status Badge (Inline Editable) -->
          {#if editingStatus}
            <div class="flex items-center gap-1">
              <select
                class="select select-xs select-bordered rounded-lg w-32"
                bind:value={$draftStatus}
                on:change={saveStatus}
                on:blur={cancelStatusEdit}
              >
                {#each SESSION_STATUSES as s}
                  <option value={s}>{statusLabel(s)}</option>
                {/each}
              </select>
            </div>
          {:else}
            <button
              class="badge badge-lg badge-primary/20 text-primary font-semibold hover:badge-primary/30 transition-colors cursor-pointer"
              on:click={startEditStatus}
              title="Click to edit status"
            >
              {statusLabel(sessionStatusValue($session))}
            </button>
          {/if}

          <!-- Date (Inline Editable) -->
          {#if editingDate}
            <div class="flex items-start gap-1">
              <input
                class="input input-xs input-bordered rounded-lg w-36"
                type="date"
                bind:value={$draftDate}
              />
              <button class="btn btn-xs btn-primary" on:click={saveDate}>
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  class="h-3 w-3"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                >
                  <path
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    stroke-width="2"
                    d="M5 13l4 4L19 7"
                  />
                </svg>
              </button>
              <button class="btn btn-xs btn-ghost" on:click={cancelDateEdit}>
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  class="h-3 w-3"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                >
                  <path
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    stroke-width="2"
                    d="M6 18L18 6M6 6l12 12"
                  />
                </svg>
              </button>
            </div>
          {:else}
            <button
              class="text-sm sm:text-base font-medium text-base-content/70 hover:text-primary transition-colors cursor-pointer"
              on:click={startEditDate}
              title="Click to edit date"
            >
              {sessionDateUS($session)}
            </button>
          {/if}
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
          
          EDIT
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
