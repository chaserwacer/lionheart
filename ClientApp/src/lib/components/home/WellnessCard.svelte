<script lang="ts">
  import { createEventDispatcher } from 'svelte';

  export let wellnessState = {
    motivationScore: 0,
    stressScore: 0,
    moodScore: 0,
    energyScore: 0,
    overallScore: -1,
    date: "",
  };

  const dispatch = createEventDispatcher();
  let modalOpen = false;

  function openModal() {
    modalOpen = true;
    dispatch('open');
  }

  function closeModal() {
    modalOpen = false;
  }

  function getScoreColor(score: number): string {
    if (score >= 4) return 'text-success';
    if (score >= 3) return 'text-warning';
    return 'text-error';
  }

  function getScoreLabel(score: number): string {
    if (score >= 4.5) return 'Excellent';
    if (score >= 4) return 'Great';
    if (score >= 3) return 'Good';
    if (score >= 2) return 'Fair';
    return 'Low';
  }
</script>

<!-- Card -->
<button
  on:click={openModal}
  class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
         cursor-pointer p-6 text-left w-full h-full
         border-2 border-base-content/10 hover:border-base-content/30"
>
  <div class="flex items-start justify-between mb-6">
    <div class="flex flex-col gap-1">
      <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Wellness</span>
      <h3 class="text-2xl font-display font-black tracking-tight">Score</h3>
    </div>
    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-6 h-6 text-base-content/30">
      <path stroke-linecap="round" stroke-linejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5" />
    </svg>
  </div>

  {#if wellnessState.overallScore === -1}
    <div class="text-center py-4">
      <p class="text-4xl font-bold text-base-content/30">--</p>
      <p class="text-sm text-base-content/50 mt-1">No data</p>
    </div>
  {:else}
    <div class="">
      <p class="text-7xl font-display font-black {getScoreColor(wellnessState.overallScore)} leading-none">{wellnessState.overallScore.toFixed(1)}</p>
      <p class="text-xs font-bold uppercase tracking-widest text-base-content/50 mt-3">{getScoreLabel(wellnessState.overallScore)}</p>
    </div>

    <div class="mt-6 grid grid-cols-2 gap-3 text-xs">
      <div class="flex flex-col gap-1">
        <span class="font-bold uppercase tracking-wider text-base-content/50">Energy</span>
        <span class="text-2xl font-display font-black">{wellnessState.energyScore}</span>
      </div>
      <div class="flex flex-col gap-1">
        <span class="font-bold uppercase tracking-wider text-base-content/50">Mood</span>
        <span class="text-2xl font-display font-black">{wellnessState.moodScore}</span>
      </div>
      <div class="flex flex-col gap-1">
        <span class="font-bold uppercase tracking-wider text-base-content/50">Motivation</span>
        <span class="text-2xl font-display font-black">{wellnessState.motivationScore}</span>
      </div>
      <div class="flex flex-col gap-1">
        <span class="font-bold uppercase tracking-wider text-base-content/50">Stress</span>
        <span class="text-2xl font-display font-black">{wellnessState.stressScore}</span>
      </div>
    </div>
  {/if}
</button>

<!-- Modal -->
<dialog class="modal" class:modal-open={modalOpen}>
  <div class="modal-box max-w-2xl bg-base-100 p-0 overflow-hidden border-2 border-base-content/20">
    <!-- Header -->
    <div class="p-6 pb-4 border-b-2 border-base-content/10">
      <div class="flex items-center justify-between">
        <div>
          <h3 class="text-3xl font-display font-black tracking-tight">Wellness Overview</h3>
          <p class="text-xs font-mono uppercase tracking-widest text-base-content/50 mt-1">Past 7 days trend</p>
        </div>
        <button on:click={closeModal} class="btn btn-ghost btn-sm btn-circle">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-5 h-5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
          </svg>
        </button>
      </div>
    </div>

    <!-- Content -->
    <div class="p-6">
     

      <!-- Score breakdown -->
      <div class="mt-6 grid grid-cols-2 gap-4">
        <div class="p-5 bg-base-200 border-2 border-base-content/10">
          <div class="flex items-center justify-between mb-3">
            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Energy</span>
            <span class="text-3xl font-display font-black {getScoreColor(wellnessState.energyScore)}">{wellnessState.energyScore}</span>
          </div>
          <div class="h-2 bg-base-300 overflow-hidden">
            <div class="h-full bg-success transition-all duration-300" style="width: {(wellnessState.energyScore / 5) * 100}%"></div>
          </div>
        </div>

        <div class="p-5 bg-base-200 border-2 border-base-content/10">
          <div class="flex items-center justify-between mb-3">
            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Mood</span>
            <span class="text-3xl font-display font-black {getScoreColor(wellnessState.moodScore)}">{wellnessState.moodScore}</span>
          </div>
          <div class="h-2 bg-base-300 overflow-hidden">
            <div class="h-full bg-info transition-all duration-300" style="width: {(wellnessState.moodScore / 5) * 100}%"></div>
          </div>
        </div>

        <div class="p-5 bg-base-200 border-2 border-base-content/10">
          <div class="flex items-center justify-between mb-3">
            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Motivation</span>
            <span class="text-3xl font-display font-black {getScoreColor(wellnessState.motivationScore)}">{wellnessState.motivationScore}</span>
          </div>
          <div class="h-2 bg-base-300 overflow-hidden">
            <div class="h-full bg-warning transition-all duration-300" style="width: {(wellnessState.motivationScore / 5) * 100}%"></div>
          </div>
        </div>

        <div class="p-5 bg-base-200 border-2 border-base-content/10">
          <div class="flex items-center justify-between mb-3">
            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Stress</span>
            <span class="text-3xl font-display font-black {getScoreColor(wellnessState.stressScore)}">{wellnessState.stressScore}</span>
          </div>
          <div class="h-2 bg-base-300 overflow-hidden">
            <div class="h-full bg-error transition-all duration-300" style="width: {(wellnessState.stressScore / 5) * 100}%"></div>
          </div>
        </div>
      </div>
    </div>
  </div>
  <form method="dialog" class="modal-backdrop bg-black/50 backdrop-blur-sm">
    <button on:click={closeModal}>close</button>
  </form>
</dialog>
