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

  let sessionId = '';
  $: ({ sessionId } = $page.params as any);

  let showAddModal = false;
  let showAiModal = false;

  onMount(() => {
    loadSession(sessionId);
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
    loadSession(sessionId);
  }
</script>

<svelte:head>
  <title>Session - Lionheart</title>
</svelte:head>

<div class={`min-h-screen bg-gradient-to-b from-base-200 to-base-300 ${$isEditing ? 'editing' : ''}`}>
  <div class="max-w-4xl mx-auto px-4 sm:px-6 py-8 pb-32">
    <SessionHeader {sessionId} />

    {#if $isLoading}
      <div class="flex items-center justify-center py-16">
        <div class="flex flex-col items-center gap-4">
          <span class="loading loading-spinner loading-lg text-primary"></span>
          <span class="text-base-content/60 font-medium">Loading session...</span>
        </div>
      </div>
    {:else if $errorMsg}
      <div class="alert alert-error shadow-lg rounded-2xl">
        <svg xmlns="http://www.w3.org/2000/svg" class="stroke-current shrink-0 h-6 w-6" fill="none" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
        <span>{$errorMsg}</span>
      </div>
    {:else if $session}
      <SessionDetailsPanel />

      <!-- Movements Section -->
      <section class="mt-8">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-sm font-semibold uppercase tracking-wider text-base-content/50">
            Movements
          </h2>
          <span class="text-xs font-mono text-base-content/40">
            {$movements?.length ?? 0} total
          </span>
        </div>

        {#if ($movements?.length ?? 0) === 0}
          <div class="card bg-base-100/50 backdrop-blur border border-dashed border-base-content/20 rounded-2xl">
            <div class="card-body items-center text-center py-12">
              <div class="w-16 h-16 rounded-full bg-base-200 flex items-center justify-center mb-4">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8 text-base-content/30" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6v6m0 0v6m0-6h6m-6 0H6" />
                </svg>
              </div>
              <h3 class="font-display font-bold text-xl text-base-content/80">No movements yet</h3>
              <p class="text-base-content/50 max-w-xs">
                Add your first movement to start building this session.
              </p>
            </div>
          </div>
        {:else}
          <div class="space-y-4">
            {#each $movements as m (idOfMovement(m))}
              <MovementCard movement={m} />
            {/each}
          </div>

          {#if $pendingOrderIds}
            <div class="mt-4 flex items-center gap-2 text-sm text-warning">
              <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
              </svg>
              <span>Order changed — save with <kbd class="kbd kbd-xs">Done</kbd></span>
            </div>
          {/if}
        {/if}

        <!-- Quick Add Movement -->
        <div data-quick-add class="mt-6">
          <QuickAddMovement {sessionId} />
        </div>
      </section>
    {:else}
      <div class="card bg-base-100 shadow-xl rounded-2xl">
        <div class="card-body items-center text-center py-16">
          <div class="w-16 h-16 rounded-full bg-error/10 flex items-center justify-center mb-4">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8 text-error" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.172 16.172a4 4 0 015.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <h3 class="font-display font-bold text-xl">Session not found</h3>
          <p class="text-base-content/50">This session may have been deleted or doesn't exist.</p>
        </div>
      </div>
    {/if}
  </div>
</div>

<!-- Floating Action Buttons -->
<div class="fixed bottom-6 right-6 z-50 flex flex-col gap-3">
  <!-- AI Modify Button -->
  <button
    on:click={() => (showAiModal = true)}
    class="btn btn-circle btn-secondary shadow-xl hover:shadow-2xl hover:scale-105 transition-all duration-200 group"
    title="Modify with AI"
  >
    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 group-hover:rotate-12 transition-transform" fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
    </svg>
  </button>
  
  <!-- Add Movement Button -->
  <button
    on:click={handleOpenAddMovement}
    class="btn btn-circle btn-primary btn-lg shadow-xl hover:shadow-2xl hover:scale-105 transition-all duration-200"
    title="Add Movement"
  >
    <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" />
    </svg>
  </button>
</div>

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
