<script lang="ts">
  import { createEventDispatcher } from 'svelte';
  import { goto } from '$app/navigation';
  import { pageUpdate } from '$lib/stores/stores';
  import {
    AddWellnessStateEndpointClient,
    CreateWellnessStateRequest
  } from '$lib/api/ApiClient';

  export let wellnessState = {
    motivationScore: 0,
    stressScore: 0,
    moodScore: 0,
    energyScore: 0,
    overallScore: -1,
    date: "",
  };

  const dispatch = createEventDispatcher();
  const baseUrl = "";
  let modalOpen = false;
  let logModalOpen = false;
  let submitting = false;

  // Form state for logging wellness
  let wellnessForm = {
    date: wellnessState.date || new Date().toISOString().split("T")[0],
    energy: 3,
    motivation: 3,
    mood: 3,
    stress: 3
  };

  // Parse date string as local date (not UTC)
  function parseLocalDate(dateStr: string): Date {
    const [year, month, day] = dateStr.split('-').map(Number);
    return new Date(year, month - 1, day);
  }

  function formatDateDisplay(dateStr: string): string {
    if (dateStr.includes('-') && dateStr.length === 10) {
      const d = parseLocalDate(dateStr);
      return d.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric", timeZone: "UTC" });
    }
    // Fallback for other formats - use UTC
    const d = new Date(dateStr);
    return d.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric", timeZone: "UTC" });
  }

  function openModal() {
    modalOpen = true;
    dispatch('open');
  }

  function closeModal() {
    modalOpen = false;
  }

  function openLogModal() {
    wellnessForm.date = wellnessState.date || new Date().toISOString().split("T")[0];
    logModalOpen = true;
  }

  function closeLogModal() {
    logModalOpen = false;
    resetForm();
  }

  function resetForm() {
    wellnessForm = {
      date: wellnessState.date || new Date().toISOString().split("T")[0],
      energy: 3,
      motivation: 3,
      mood: 3,
      stress: 3
    };
  }

  async function logWellness() {
    submitting = true;
    try {
      const client = new AddWellnessStateEndpointClient(baseUrl);
      const request = new CreateWellnessStateRequest({
        date: parseLocalDate(wellnessForm.date),
        energy: wellnessForm.energy,
        motivation: wellnessForm.motivation,
        mood: wellnessForm.mood,
        stress: wellnessForm.stress
      });
      
      await client.post(request);
      closeLogModal();
      pageUpdate.set(new Date());
    } catch (err) {
      console.error("Failed to log wellness:", err);
    } finally {
      submitting = false;
    }
  }

  function goToWellnessPage() {
    goto('/wellness');
  }

  function getScoreColor(score: number): string {
    if (score >= 4) return 'text-success';
    if (score >= 3) return 'text-warning';
    return 'text-error';
  }

  function getStressColor(score: number): string {
    // For stress, lower is better (inverted)
    if (score <= 2) return 'text-success';
    if (score <= 3) return 'text-warning';
    return 'text-error';
  }

  function getScoreLabel(score: number): string {
    if (score >= 4.5) return 'Excellent';
    if (score >= 4) return 'Great';
    if (score >= 3) return 'Good';
    if (score >= 2) return 'Fair';
    return 'Low';
  }

  $: calculatedOverall = ((wellnessForm.mood + wellnessForm.energy + wellnessForm.motivation + (6 - wellnessForm.stress)) / 4);
</script>

<!-- Card -->
<button
  on:click={wellnessState.overallScore === -1 ? openLogModal : openModal}
  class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
         cursor-pointer p-6 text-left w-full h-full
         border-2 {wellnessState.overallScore === -1 ? 'border-dashed border-primary/40' : 'border-base-content/10 hover:border-base-content/30'}"
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
    <div class="py-4">
      <p class="text-xl font-display font-black text-base-content/70">Log Today</p>
      <p class="text-sm text-base-content/50 mt-1">Record how you're feeling</p>
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

      <!-- View All Link -->
      <div class="mt-6 pt-4 border-t-2 border-base-content/10">
        <button
          on:click={goToWellnessPage}
          class="btn btn-ghost btn-sm w-full rounded-xl text-xs font-bold uppercase tracking-wider"
        >
          View All Wellness Data
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-4 h-4">
            <path stroke-linecap="round" stroke-linejoin="round" d="M13.5 4.5 21 12m0 0-7.5 7.5M21 12H3" />
          </svg>
        </button>
      </div>
    </div>
  </div>
  <form method="dialog" class="modal-backdrop bg-black/50 backdrop-blur-sm">
    <button on:click={closeModal}>close</button>
  </form>
</dialog>

<!-- Log Wellness Modal -->
{#if logModalOpen}
  <div class="modal modal-open">
    <div class="modal-box w-11/12 max-w-lg bg-base-100 p-0 overflow-hidden border-2 border-base-content/20">
      <!-- Header -->
      <div class="p-6 pb-4 border-b-2 border-base-content/10">
        <div class="flex items-center justify-between">
          <div>
            <h3 class="text-3xl font-display font-black tracking-tight">Log Wellness</h3>
            <p class="text-xs font-mono uppercase tracking-widest text-base-content/50 mt-1">{formatDateDisplay(wellnessForm.date)}</p>
          </div>
          <button on:click={closeLogModal} class="btn btn-ghost btn-sm btn-circle">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-5 h-5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      </div>

      <!-- Content -->
      <form on:submit|preventDefault={logWellness} class="p-6">
        <div class="form-control w-full mb-6">
          <label class="label" for="home-wellness-date">
            <span class="label-text font-bold uppercase text-xs tracking-wider">Date</span>
          </label>
          <input
            id="home-wellness-date"
            type="date"
            class="input input-bordered w-full rounded-xl"
            bind:value={wellnessForm.date}
            max={new Date().toISOString().split("T")[0]}
            required
          />
        </div>

        <div class="space-y-6">
          <!-- Energy -->
          <div class="form-control w-full">
            <div class="flex items-center justify-between mb-2">
              <label class="label p-0" for="home-energy-slider">
                <span class="label-text font-bold uppercase text-xs tracking-wider">Energy</span>
              </label>
              <span class="text-xl font-display font-black {getScoreColor(wellnessForm.energy)}">{wellnessForm.energy}/5</span>
            </div>
            <input
              id="home-energy-slider"
              type="range"
              min="1"
              max="5"
              class="range range-warning range-sm"
              bind:value={wellnessForm.energy}
            />
          </div>

          <!-- Motivation -->
          <div class="form-control w-full">
            <div class="flex items-center justify-between mb-2">
              <label class="label p-0" for="home-motivation-slider">
                <span class="label-text font-bold uppercase text-xs tracking-wider">Motivation</span>
              </label>
              <span class="text-xl font-display font-black {getScoreColor(wellnessForm.motivation)}">{wellnessForm.motivation}/5</span>
            </div>
            <input
              id="home-motivation-slider"
              type="range"
              min="1"
              max="5"
              class="range range-info range-sm"
              bind:value={wellnessForm.motivation}
            />
          </div>

          <!-- Mood -->
          <div class="form-control w-full">
            <div class="flex items-center justify-between mb-2">
              <label class="label p-0" for="home-mood-slider">
                <span class="label-text font-bold uppercase text-xs tracking-wider">Mood</span>
              </label>
              <span class="text-xl font-display font-black {getScoreColor(wellnessForm.mood)}">{wellnessForm.mood}/5</span>
            </div>
            <input
              id="home-mood-slider"
              type="range"
              min="1"
              max="5"
              class="range range-success range-sm"
              bind:value={wellnessForm.mood}
            />
          </div>

          <!-- Stress -->
          <div class="form-control w-full">
            <div class="flex items-center justify-between mb-2">
              <label class="label p-0" for="home-stress-slider">
                <span class="label-text font-bold uppercase text-xs tracking-wider">Stress</span>
              </label>
              <span class="text-xl font-display font-black {getStressColor(wellnessForm.stress)}">{wellnessForm.stress}/5</span>
            </div>
            <input
              id="home-stress-slider"
              type="range"
              min="1"
              max="5"
              class="range range-error range-sm"
              bind:value={wellnessForm.stress}
            />
          </div>
        </div>

        <!-- Preview -->
        <div class="bg-base-200 rounded-xl p-4 mt-6 border-2 border-base-content/10">
          <div class="flex items-center justify-between">
            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Calculated Overall</span>
            <div class="flex items-baseline gap-1">
              <span class="text-2xl font-display font-black {getScoreColor(calculatedOverall)}">
                {calculatedOverall.toFixed(1)}
              </span>
              <span class="text-base-content/40 font-bold">/5</span>
            </div>
          </div>
        </div>

        <div class="flex justify-end gap-2 mt-6">
          <button type="button" class="btn btn-outline px-5 rounded-xl" on:click={closeLogModal}>Cancel</button>
          <button type="submit" class="btn btn-primary px-5 rounded-xl" disabled={submitting}>
            {#if submitting}
              <span class="loading loading-spinner loading-sm"></span>
            {/if}
            Log Wellness
          </button>
        </div>
      </form>
    </div>
    <div class="modal-backdrop bg-base-300/80" on:click={closeLogModal} on:keydown={(e) => e.key === 'Escape' && closeLogModal()} role="button" tabindex="0" aria-label="Close modal"></div>
  </div>
{/if}
