<script lang="ts">
  import type { TrainingSessionDTO } from '$lib/api/ApiClient';

  export let session: TrainingSessionDTO | null = null;

  let modalOpen = false;

  function openModal() {
    if (session) modalOpen = true;
  }

  function closeModal() {
    modalOpen = false;
  }

  function formatDate(date: Date | undefined): string {
    if (!date) return 'Not scheduled';
    const d = new Date(date);
    const today = new Date();
    const tomorrow = new Date(today);
    tomorrow.setDate(tomorrow.getDate() + 1);

    if (d.toDateString() === today.toDateString()) return 'Today';
    if (d.toDateString() === tomorrow.toDateString()) return 'Tomorrow';

    return d.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' });
  }

  function getSessionTitle(s: TrainingSessionDTO | null): string {
    if (!s) return 'Session';
    if (s.notes) return s.notes.substring(0, 30);
    return 'Training Session';
  }

  $: movementCount = session?.movements?.length ?? 0;
</script>

<!-- Card -->
<button
  on:click={openModal}
  class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
         cursor-pointer p-6 text-left w-full h-full
         border-2 border-base-content/10 hover:border-base-content/30
         {!session ? 'opacity-60' : ''}"
  disabled={!session}
>
  <div class="flex items-start justify-between mb-6">
    <div class="flex flex-col gap-1">
      <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Upcoming</span>
      <h3 class="text-2xl font-display font-black tracking-tight">Session</h3>
    </div>
    {#if session}
      <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-6 h-6 text-base-content/30">
        <path stroke-linecap="round" stroke-linejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5" />
      </svg>
    {/if}
  </div>

  {#if session}
    <div>
      <p class="text-xl font-bold uppercase tracking-wide truncate">{getSessionTitle(session)}</p>
      <p class="text-sm font-mono uppercase tracking-widest text-base-content/50 mt-2">{formatDate(session.date)}</p>
    </div>

    <div class="mt-6 pt-4 border-t-2 border-base-content/10">
      <div class="flex items-baseline gap-2">
        <span class="text-4xl font-display font-black">{movementCount}</span>
        <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Movements</span>
      </div>
    </div>
  {:else}
    <div class="py-4">
      <p class="text-base-content/50">No upcoming session</p>
      <p class="text-xs text-base-content/30 mt-1">Create a training program</p>
    </div>
  {/if}
</button>

<!-- Modal -->
<dialog class="modal" class:modal-open={modalOpen}>
  <div class="modal-box rounded-3xl max-w-2xl bg-base-100 p-0 overflow-hidden max-h-[90vh]">
    <!-- Header -->
    <div class="p-6 pb-4 border-b border-base-200">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="w-12 h-12 rounded-2xl bg-success/10 flex items-center justify-center">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-6 h-6 text-success">
              <path stroke-linecap="round" stroke-linejoin="round" d="M3 13.125C3 12.504 3.504 12 4.125 12h2.25c.621 0 1.125.504 1.125 1.125v6.75C7.5 20.496 6.996 21 6.375 21h-2.25A1.125 1.125 0 0 1 3 19.875v-6.75ZM9.75 8.625c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125v11.25c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V8.625ZM16.5 4.125c0-.621.504-1.125 1.125-1.125h2.25C20.496 3 21 3.504 21 4.125v15.75c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 0 1-1.125-1.125V4.125Z" />
            </svg>
          </div>
          <div>
            <h3 class="font-semibold text-lg">{getSessionTitle(session)}</h3>
            <p class="text-sm text-base-content/50">{formatDate(session?.date)}</p>
          </div>
        </div>
        <button on:click={closeModal} class="btn btn-ghost btn-sm btn-circle">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
          </svg>
        </button>
      </div>
    </div>

    <!-- Content -->
    <div class="p-6 overflow-y-auto max-h-[60vh]">
      <h4 class="font-medium mb-4">Planned Movements ({movementCount})</h4>

      {#if session?.movements && session.movements.length > 0}
        <div class="space-y-3">
          {#each session.movements as movement, index}
            <div class="p-4 bg-base-200 rounded-2xl">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-3">
                  <span class="w-6 h-6 rounded-full bg-success/20 text-success text-xs font-medium flex items-center justify-center">
                    {index + 1}
                  </span>
                  <span class="font-medium">{movement.movementData?.movementBase?.name || 'Unknown'}</span>
                </div>
              </div>
              {#if movement.movementData?.equipment?.name}
                <p class="text-sm text-base-content/50 mt-2 ml-9">{movement.movementData.equipment.name}</p>
              {/if}
            </div>
          {/each}
        </div>
      {:else}
        <div class="text-center py-8 text-base-content/50">
          <p>No movements planned yet</p>
        </div>
      {/if}

      <div class="mt-6">
        <a href="/training" class="btn btn-success btn-block rounded-xl">
          Go to Training
        </a>
      </div>
    </div>
  </div>
  <form method="dialog" class="modal-backdrop bg-black/50 backdrop-blur-sm">
    <button on:click={closeModal}>close</button>
  </form>
</dialog>
