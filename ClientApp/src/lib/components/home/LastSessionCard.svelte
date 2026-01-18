<script lang="ts">
  import type { TrainingSessionDTO, LiftSetEntryDTO } from '$lib/api/ApiClient';

  export let session: TrainingSessionDTO | null = null;

  let modalOpen = false;

  function openModal() {
    if (session) modalOpen = true;
  }

  function closeModal() {
    modalOpen = false;
  }

  function formatDate(date: Date | undefined): string {
    if (!date) return 'Unknown';
    const d = new Date(date);
    const today = new Date();
    const diffTime = today.getTime() - d.getTime();
    const diffDays = Math.floor(diffTime / (1000 * 60 * 60 * 24));

    if (diffDays === 0) return 'Today';
    if (diffDays === 1) return 'Yesterday';
    if (diffDays < 7) return `${diffDays} days ago`;
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  }

  function isWithinTwoWeeks(date: Date | undefined): boolean {
    if (!date) return false;
    const d = new Date(date);
    const today = new Date();
    const diffTime = today.getTime() - d.getTime();
    const diffDays = diffTime / (1000 * 60 * 60 * 24);
    return diffDays <= 14;
  }

  $: movementCount = session?.movements?.length ?? 0;
  $: shouldShow = session && isWithinTwoWeeks(session.date);

  // Calculate totals from movements
  $: totalSets = session?.movements?.reduce((sum, m) => {
    const liftSets = m.liftSets?.length ?? 0;
    const dtSets = m.distanceTimeSets?.length ?? 0;
    return sum + liftSets + dtSets;
  }, 0) ?? 0;

  $: totalVolume = session?.movements?.reduce((sum, m) => {
    const liftVolume = m.liftSets?.reduce((s: number, e: LiftSetEntryDTO) => s + ((e.actualWeight ?? 0) * (e.actualReps ?? 0)), 0) ?? 0;
    return sum + liftVolume;
  }, 0) ?? 0;

  function getSessionTitle(s: TrainingSessionDTO | null): string {
    if (!s) return 'Session';
    if (s.notes) return s.notes.substring(0, 30);
    return 'Training Session';
  }
</script>

<!-- Card -->
<button
  on:click={openModal}
  class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
         cursor-pointer p-6 text-left w-full h-full
         border-2 border-base-content/10 hover:border-base-content/30
         {!shouldShow ? 'opacity-60' : ''}"
  disabled={!shouldShow}
>
  <div class="flex items-start justify-between mb-6">
    <div class="flex flex-col gap-1">
      <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Completed</span>
      <h3 class="text-2xl font-display font-black tracking-tight">Session</h3>
    </div>
    {#if shouldShow}
      <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-6 h-6 text-base-content/30">
        <path stroke-linecap="round" stroke-linejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5" />
      </svg>
    {/if}
  </div>

  {#if shouldShow && session}
    <div>
      <p class="text-xl font-bold uppercase tracking-wide truncate">{getSessionTitle(session)}</p>
      <p class="text-sm font-mono uppercase tracking-widest text-base-content/50 mt-2">{formatDate(session.date)}</p>
    </div>

    <div class="mt-6 pt-4 border-t-2 border-base-content/10 grid grid-cols-2 gap-4">
      <div class="flex flex-col gap-1">
        <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Moves</span>
        <span class="text-3xl font-display font-black">{movementCount}</span>
      </div>
      {#if totalSets > 0}
        <div class="flex flex-col gap-1">
          <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Sets</span>
          <span class="text-3xl font-display font-black">{totalSets}</span>
        </div>
      {/if}
    </div>
  {:else}
    <div class="text-center py-4">
      <p class="text-base-content/50">No recent session</p>
      <p class="text-xs text-base-content/30 mt-1">Within past 2 weeks</p>
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
          <div class="w-12 h-12 rounded-2xl bg-info/10 flex items-center justify-center">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-6 h-6 text-info">
              <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
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
      <!-- Summary Stats -->
      <div class="grid grid-cols-3 gap-4 mb-6">
        <div class="bg-base-200 rounded-2xl p-4 text-center">
          <p class="text-2xl font-bold">{movementCount}</p>
          <p class="text-xs text-base-content/50">Movements</p>
        </div>
        <div class="bg-base-200 rounded-2xl p-4 text-center">
          <p class="text-2xl font-bold">{totalSets}</p>
          <p class="text-xs text-base-content/50">Total Sets</p>
        </div>
        <div class="bg-base-200 rounded-2xl p-4 text-center">
          <p class="text-2xl font-bold">{totalVolume > 1000 ? (totalVolume / 1000).toFixed(1) + 'k' : totalVolume}</p>
          <p class="text-xs text-base-content/50">Volume (lbs)</p>
        </div>
      </div>

      <!-- Perceived Effort -->
      {#if session?.perceivedEffortRatings}
        <div class="mb-6">
          <h4 class="font-medium mb-3">Perceived Effort</h4>
          <div class="p-4 bg-base-200 rounded-2xl">
            <div class="flex items-center justify-between">
              <span>Difficulty</span>
              <div class="flex items-center gap-2">
                <progress class="progress progress-info w-24 h-2" value={session.perceivedEffortRatings.difficultyRating ?? 0} max="10"></progress>
                <span class="font-bold">{session.perceivedEffortRatings.difficultyRating ?? 0}/10</span>
              </div>
            </div>
          </div>
        </div>
      {/if}

      <!-- Movements List -->
      <h4 class="font-medium mb-3">Movements</h4>
      {#if session?.movements && session.movements.length > 0}
        <div class="space-y-3">
          {#each session.movements as movement, index}
            <div class="p-4 bg-base-200 rounded-2xl">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-3">
                  <span class="w-6 h-6 rounded-full bg-info/20 text-info text-xs font-medium flex items-center justify-center">
                    {index + 1}
                  </span>
                  <span class="font-medium">{movement.movementData?.movementBase?.name || 'Unknown'}</span>
                </div>
                <span class="text-sm text-base-content/50">
                  {(movement.liftSets?.length ?? 0) + (movement.distanceTimeSets?.length ?? 0)} sets
                </span>
              </div>
              {#if movement.liftSets && movement.liftSets.length > 0}
                {@const maxWeight = Math.max(...movement.liftSets.map(e => e.actualWeight ?? 0))}
                {@const bestSet = movement.liftSets.find(e => e.actualWeight === maxWeight)}
                <div class="mt-2 ml-9 text-sm text-base-content/60">
                  Best: {maxWeight} lbs x {bestSet?.actualReps ?? 0}
                </div>
              {/if}
            </div>
          {/each}
        </div>
      {:else}
        <div class="text-center py-8 text-base-content/50">
          <p>No movement data recorded</p>
        </div>
      {/if}
    </div>
  </div>
  <form method="dialog" class="modal-backdrop bg-black/50 backdrop-blur-sm">
    <button on:click={closeModal}>close</button>
  </form>
</dialog>
