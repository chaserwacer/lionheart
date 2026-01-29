<script lang="ts">
  import { page } from '$app/stores';
  import { onMount, onDestroy } from 'svelte';

  import {
    session,
    movements,
    isLoading,
    errorMsg,
    isEditing,
    pendingOrderIds,
    loadSession,
    resetSessionStore,
    enterEditMode,
  } from '$lib/stores/sessionStore';
  import { idOfMovement } from '$lib/utils/training';

  import SessionHeader from '$lib/components/training/SessionHeader.svelte';
  import SessionDetailsPanel from '$lib/components/training/SessionDetailsPanel.svelte';
  import MovementCard from '$lib/components/training/MovementCard.svelte';
  import QuickAddMovement from '$lib/components/training/QuickAddMovement.svelte';
  import ModifyTrainingSessionModal from '$lib/components/modals/ModifyTrainingSessionModal.svelte';

  let programId = '';
  let sessionId = '';
  $: ({ programId, sessionId } = $page.params as any);

  let showAiModal = false;

  onMount(() => {
    loadSession(sessionId, programId);
  });

  onDestroy(() => {
    resetSessionStore();
  });

  function handleOpenAddMovement() {
    enterEditMode();
    // Scroll to the quick add section
    setTimeout(() => {
      const quickAdd = document.querySelector('[data-quick-add]');
      quickAdd?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }, 100);
  }

  function handleAiModalClose() {
    showAiModal = false;
    loadSession(sessionId, programId);
  }
</script>

<svelte:head>
  <title>Session - Lionheart</title>
</svelte:head>

<div class={`min-h-full bg-base-200 ${$isEditing ? 'editing' : ''}`}>
  <div class="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
    <SessionHeader {programId} {sessionId} />

    {#if $isLoading}
      <div class="card bg-base-100 p-8 border border-base-content/10 rounded-2xl text-lg">
        Loading session...
      </div>
    {:else if $errorMsg}
      <div class="alert alert-error rounded-xl">
        <span>{$errorMsg}</span>
      </div>
    {:else if $session}
      <SessionDetailsPanel />

      <!-- Movements -->
      {#if ($movements?.length ?? 0) === 0}
        <div class="p-6 bg-base-200 rounded-xl">
          <div class="font-display font-black text-xl mb-2">No movements yet</div>
          <div class="text-base text-base-content/60">
            Add your first movement below.
          </div>
        </div>
      {:else}
        <div class="space-y-3">
          {#each $movements as m (idOfMovement(m))}
            <MovementCard movement={m} />
          {/each}
        </div>

        {#if $pendingOrderIds}
          <div class="mt-4 text-sm text-base-content/60">
            Order changed — will save when you hit <span class="font-bold">Done</span>.
          </div>
        {/if}
      {/if}

      <!-- Quick Add Movement - always visible -->
      <div data-quick-add>
        <QuickAddMovement {sessionId} />
      </div>
    {:else}
      <div class="card bg-base-100 p-8 border border-base-content/10 rounded-2xl">
        Session not found.
      </div>
    {/if}
  </div>
</div>

<!-- Floating Add Movement Button -->
<button
  on:click={handleOpenAddMovement}
  class="fixed bottom-6 right-6 btn btn-primary btn-circle text-2xl shadow-lg z-50 hover:scale-110 transition-transform"
  title="Add Movement"
>
  +
</button>

<!-- Floating AI Modify Button -->
<button
  on:click={() => (showAiModal = true)}
  class="fixed bottom-20 right-6 btn btn-secondary btn-circle text-sm font-bold shadow-lg z-50 hover:scale-110 transition-transform"
  title="Modify with AI"
>
  AI
</button>

<!-- AI Modification Modal -->
{#if showAiModal && $session}
  <ModifyTrainingSessionModal
    show={showAiModal}
    session={$session}
    on:close={handleAiModalClose}
  />
{/if}

<style>
  :global(.editing) :global(.wiggle) {
    animation: wiggle 0.18s infinite alternate ease-in-out;
    transform-origin: 50% 50%;
  }

  @keyframes wiggle {
    from {
      transform: rotate(-0.6deg);
    }
    to {
      transform: rotate(0.6deg);
    }
  }
</style>
