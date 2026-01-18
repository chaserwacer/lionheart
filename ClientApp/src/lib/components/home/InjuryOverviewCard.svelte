<script lang="ts">
  import { onMount } from 'svelte';
  import { GetInjuriesEndpointClient, type InjuryDTO } from '$lib/api/ApiClient';

  export let baseUrl = '';

  let injuries: InjuryDTO[] = [];
  let activeInjuries: InjuryDTO[] = [];
  let loading = true;
  let modalOpen = false;

  $: activeInjuries = (injuries ?? []).filter(i => i.isActive);
  $: mostRecentEvent = getRecentEvent();

  onMount(async () => {
    await loadInjuries();
  });

  async function loadInjuries() {
    if (!baseUrl) {
      baseUrl = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5174';
    }
    try {
      const client = new GetInjuriesEndpointClient(baseUrl);
      const result = await client.get();
      injuries = Array.isArray(result) ? result : [];
    } catch (e) {
      console.error('Failed to load injuries', e);
      injuries = [];
    } finally {
      loading = false;
    }
  }

  function getRecentEvent() {
    if (!activeInjuries || !Array.isArray(activeInjuries)) return null;

    const twoWeeksAgo = new Date();
    twoWeeksAgo.setDate(twoWeeksAgo.getDate() - 14);

    for (const injury of activeInjuries) {
      if (injury.injuryEvents) {
        for (const event of injury.injuryEvents) {
          if (event.creationTime && new Date(event.creationTime) >= twoWeeksAgo) {
            return { injury, event };
          }
        }
      }
    }
    return null;
  }

  function openModal() {
    if (activeInjuries.length > 0 || mostRecentEvent) modalOpen = true;
  }

  function closeModal() {
    modalOpen = false;
  }

  function formatDate(date: Date | undefined): string {
    if (!date) return 'Unknown';
    const d = new Date(date);
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  }

  function getPainColor(painLevel: number | undefined): string {
    if (!painLevel) return 'badge-ghost';
    if (painLevel >= 7) return 'badge-error';
    if (painLevel >= 4) return 'badge-warning';
    return 'badge-success';
  }

  function getMaxPainLevel(injury: InjuryDTO): number {
    if (!injury.injuryEvents || injury.injuryEvents.length === 0) return 0;
    return Math.max(...injury.injuryEvents.map(e => e.painLevel || 0));
  }
</script>

<!-- Card -->
<button
  on:click={openModal}
  class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
         cursor-pointer p-6 text-left w-full h-full
         border-2 border-base-content/10 hover:border-base-content/30
         {activeInjuries.length === 0 ? 'opacity-60' : ''}"
  disabled={activeInjuries.length === 0}
>
  <div class="flex items-start justify-between mb-6">
    <div class="flex flex-col gap-1">
      <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Status</span>
      <h3 class="text-2xl font-display font-black tracking-tight">Injuries</h3>
    </div>
    {#if activeInjuries.length > 0}
      <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-6 h-6 text-base-content/30">
        <path stroke-linecap="round" stroke-linejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5" />
      </svg>
    {/if}
  </div>

  {#if loading}
    <div class="text-center py-4">
      <span class="loading loading-spinner loading-sm"></span>
    </div>
  {:else if activeInjuries.length > 0}
    <div>
      <div class="flex items-baseline gap-3">
        <p class="text-7xl font-display font-black text-error leading-none">{activeInjuries.length}</p>
        <p class="text-xs font-bold uppercase tracking-widest text-base-content/50">Active</p>
      </div>

      {#if mostRecentEvent}
        <div class="mt-6 pt-4 border-t-2 border-base-content/10">
          <p class="text-sm font-bold uppercase tracking-wide truncate">{mostRecentEvent.injury.name}</p>
          <p class="text-xs text-base-content/50 truncate mt-1">{mostRecentEvent.event.notes || 'Event logged'}</p>
        </div>
      {/if}
    </div>
  {:else}
    <div class="py-4">
      <p class="text-7xl font-display font-black text-success leading-none">0</p>
      <p class="text-xs font-bold uppercase tracking-widest text-success mt-3">All clear!</p>
    </div>
  {/if}
</button>

<!-- Modal -->
<dialog class="modal" class:modal-open={modalOpen}>
  <div class="modal-box max-w-2xl bg-base-100 p-0 overflow-hidden max-h-[90vh] border-2 border-base-content/20">
    <!-- Header -->
    <div class="p-6 pb-4 border-b-2 border-base-content/10">
      <div class="flex items-center justify-between">
        <div>
          <h3 class="text-3xl font-display font-black tracking-tight">Injury Overview</h3>
          <p class="text-xs font-mono uppercase tracking-widest text-base-content/50 mt-1">{activeInjuries.length} active {activeInjuries.length === 1 ? 'injury' : 'injuries'}</p>
        </div>
        <button on:click={closeModal} class="btn btn-ghost btn-sm btn-circle">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-5 h-5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
          </svg>
        </button>
      </div>
    </div>

    <!-- Content -->
    <div class="p-6 overflow-y-auto max-h-[60vh]">
      {#if activeInjuries.length > 0}
        <div class="space-y-4">
          {#each activeInjuries as injury}
            <div class="p-5 bg-base-200 border-2 border-base-content/10">
              <div class="flex items-start justify-between mb-3">
                <div>
                  <h4 class="text-xl font-bold uppercase tracking-wide">{injury.name}</h4>
                  <p class="text-xs font-mono uppercase tracking-widest text-base-content/50 mt-1">Started {formatDate(injury.injuryDate)}</p>
                </div>
                <span class="px-3 py-1 {getPainColor(getMaxPainLevel(injury)).replace('badge-', 'bg-')} text-xs font-bold uppercase tracking-wider">
                  Pain {getMaxPainLevel(injury)}/10
                </span>
              </div>

              {#if injury.notes}
                <p class="text-sm text-base-content/70 mb-3">{injury.notes}</p>
              {/if}

              <!-- Recent Events -->
              {#if injury.injuryEvents && injury.injuryEvents.length > 0}
                <div class="mt-4 pt-4 border-t-2 border-base-content/10">
                  <p class="text-xs font-bold uppercase tracking-widest text-base-content/50 mb-3">Recent Events</p>
                  <div class="space-y-2">
                    {#each injury.injuryEvents.slice(0, 3) as event}
                      <div class="flex items-center justify-between text-sm">
                        <span class="text-base-content/70">{event.notes || 'Event'}</span>
                        <span class="text-base-content/50">{formatDate(event.creationTime)}</span>
                      </div>
                    {/each}
                  </div>
                </div>
              {/if}
            </div>
          {/each}
        </div>
      {:else}
        <div class="text-center py-8">
          <div class="w-16 h-16 mx-auto mb-4 rounded-full bg-success/10 flex items-center justify-center">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-8 h-8 text-success">
              <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
            </svg>
          </div>
          <p class="text-base-content/50">No active injuries</p>
          <p class="text-sm text-success mt-1">You're all clear!</p>
        </div>
      {/if}
    </div>
  </div>
  <form method="dialog" class="modal-backdrop bg-black/50 backdrop-blur-sm">
    <button on:click={closeModal}>close</button>
  </form>
</dialog>
