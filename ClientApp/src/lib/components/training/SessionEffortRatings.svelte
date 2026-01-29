<script lang="ts">
  import { page } from '$app/stores';
  import {
    session,
    isLoading,
    updateSessionEffortRatings,
  } from '$lib/stores/sessionStore';
  import { PerceivedEffortRatings } from '$lib/api/ApiClient';

  let sessionId = '';
  $: ({ sessionId } = $page.params as { sessionId: string });

  let editing = false;
  let includeEffortRatings = false;
  let draftRatings = {
    difficultyRating: 3,
    engagementRating: 3,
    externalVariablesRating: 3,
    accumulatedFatigue: 3,
  };

  // Reactive variable for current ratings from session
  $: currentRatings = ($session as any)?.perceivedEffortRatings as PerceivedEffortRatings | undefined;
  $: hasRatings = currentRatings != null;

  function startEditing() {
    if (currentRatings) {
      includeEffortRatings = true;
      draftRatings = {
        difficultyRating: currentRatings.difficultyRating ?? 3,
        engagementRating: currentRatings.engagementRating ?? 3,
        externalVariablesRating: currentRatings.externalVariablesRating ?? 3,
        accumulatedFatigue: currentRatings.accumulatedFatigue ?? 3,
      };
    } else {
      includeEffortRatings = false;
      draftRatings = {
        difficultyRating: 3,
        engagementRating: 3,
        externalVariablesRating: 3,
        accumulatedFatigue: 3,
      };
    }
    editing = true;
  }

  async function saveRatings() {
    if (!includeEffortRatings) {
      await updateSessionEffortRatings(sessionId, null);
    } else {
      const ratings = new PerceivedEffortRatings({
        recordedAt: new Date(),
        difficultyRating: draftRatings.difficultyRating,
        engagementRating: draftRatings.engagementRating,
        externalVariablesRating: draftRatings.externalVariablesRating,
        accumulatedFatigue: draftRatings.accumulatedFatigue,
      });
      await updateSessionEffortRatings(sessionId, ratings);
    }
    editing = false;
  }

  function cancelEditing() {
    editing = false;
  }

  function getRatingColor(rating: number | undefined): string {
    if (!rating) return 'text-base-content/30';
    if (rating >= 4) return 'text-success';
    if (rating >= 3) return 'text-warning';
    return 'text-error';
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape') {
      cancelEditing();
    }
  }
</script>

<div class="mb-6">
  {#if editing}
    <!-- Editing Mode -->
    <div class="card bg-base-100/60 backdrop-blur border border-primary/30 rounded-xl">
      <div class="card-body py-4 px-5">
        <div class="flex items-center justify-between mb-4">
          <div class="flex items-center gap-2">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-base-content/40" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
            </svg>
            <span class="text-xs font-medium uppercase tracking-wider text-base-content/40">Perceived Effort</span>
          </div>
          <label class="label cursor-pointer gap-3">
            <span class="label-text text-xs font-bold uppercase tracking-wider text-base-content/60">
              {includeEffortRatings ? 'Enabled' : 'Disabled'}
            </span>
            <input
              type="checkbox"
              class="toggle toggle-primary toggle-sm"
              bind:checked={includeEffortRatings}
            />
          </label>
        </div>

        {#if includeEffortRatings}
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-6">
            <!-- Difficulty -->
            <div class="form-control w-full">
              <label class="label" for="edit-difficulty">
                <span class="label-text font-bold uppercase text-xs tracking-wider">Difficulty</span>
                <span class="label-text-alt text-lg font-display font-black {getRatingColor(draftRatings.difficultyRating)}">{draftRatings.difficultyRating}/5</span>
              </label>
              <input
                id="edit-difficulty"
                type="range"
                min="1"
                max="5"
                class="range range-primary range-sm"
                bind:value={draftRatings.difficultyRating}
                on:keydown={handleKeydown}
              />
              <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/50">
                <span>Easy</span>
                <span>Hard</span>
              </div>
            </div>

            <!-- Engagement -->
            <div class="form-control w-full">
              <label class="label" for="edit-engagement">
                <span class="label-text font-bold uppercase text-xs tracking-wider">Engagement</span>
                <span class="label-text-alt text-lg font-display font-black {getRatingColor(draftRatings.engagementRating)}">{draftRatings.engagementRating}/5</span>
              </label>
              <input
                id="edit-engagement"
                type="range"
                min="1"
                max="5"
                class="range range-info range-sm"
                bind:value={draftRatings.engagementRating}
                on:keydown={handleKeydown}
              />
              <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/50">
                <span>Low</span>
                <span>High</span>
              </div>
            </div>

            <!-- External Variables -->
            <div class="form-control w-full">
              <label class="label" for="edit-external">
                <span class="label-text font-bold uppercase text-xs tracking-wider">External Variables</span>
                <span class="label-text-alt text-lg font-display font-black {getRatingColor(draftRatings.externalVariablesRating)}">{draftRatings.externalVariablesRating}/5</span>
              </label>
              <input
                id="edit-external"
                type="range"
                min="1"
                max="5"
                class="range range-warning range-sm"
                bind:value={draftRatings.externalVariablesRating}
                on:keydown={handleKeydown}
              />
              <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/50">
                <span>Minimal</span>
                <span>Significant</span>
              </div>
            </div>

            <!-- Accumulated Fatigue -->
            <div class="form-control w-full">
              <label class="label" for="edit-fatigue">
                <span class="label-text font-bold uppercase text-xs tracking-wider">Accumulated Fatigue</span>
                <span class="label-text-alt text-lg font-display font-black {getRatingColor(draftRatings.accumulatedFatigue)}">{draftRatings.accumulatedFatigue}/5</span>
              </label>
              <input
                id="edit-fatigue"
                type="range"
                min="1"
                max="5"
                class="range range-error range-sm"
                bind:value={draftRatings.accumulatedFatigue}
                on:keydown={handleKeydown}
              />
              <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/50">
                <span>Fresh</span>
                <span>Exhausted</span>
              </div>
            </div>
          </div>
        {:else}
          <p class="text-sm text-base-content/50 italic">Toggle on to track how the session felt</p>
        {/if}

        <div class="flex justify-end gap-2 mt-4">
          <button class="btn btn-ghost btn-xs" on:click={cancelEditing} disabled={$isLoading}>
            Cancel
          </button>
          <button class="btn btn-primary btn-xs" on:click={saveRatings} disabled={$isLoading}>
            {#if $isLoading}
              <span class="loading loading-spinner loading-xs"></span>
            {:else}
              Save
            {/if}
          </button>
        </div>
      </div>
    </div>
  {:else if hasRatings && currentRatings}
    <!-- Display Mode - Has Ratings -->
    <button
      class="card bg-base-100/60 backdrop-blur border border-base-content/5 rounded-xl w-full text-left hover:border-primary/30 transition-colors cursor-pointer"
      on:click={startEditing}
      title="Click to edit perceived effort"
    >
      <div class="card-body py-4 px-5">
        <div class="flex items-center gap-2 mb-3">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-base-content/40" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
          </svg>
          <span class="text-xs font-medium uppercase tracking-wider text-base-content/40">Perceived Effort</span>
        </div>

        <div class="grid grid-cols-2 gap-4">
          <div class="flex flex-col gap-2">
            <div class="flex items-center justify-between">
              <span class="text-sm font-bold text-base-content/70">Difficulty</span>
              <span class="text-xl font-display font-black {getRatingColor(currentRatings.difficultyRating)}">
                {currentRatings.difficultyRating ?? '-'}/5
              </span>
            </div>
            <div class="h-2 bg-base-300 rounded-full overflow-hidden">
              <div
                class="h-full bg-primary transition-all duration-300"
                style="width: {((currentRatings.difficultyRating ?? 0) / 5) * 100}%"
              ></div>
            </div>
          </div>

          <div class="flex flex-col gap-2">
            <div class="flex items-center justify-between">
              <span class="text-sm font-bold text-base-content/70">Engagement</span>
              <span class="text-xl font-display font-black {getRatingColor(currentRatings.engagementRating)}">
                {currentRatings.engagementRating ?? '-'}/5
              </span>
            </div>
            <div class="h-2 bg-base-300 rounded-full overflow-hidden">
              <div
                class="h-full bg-info transition-all duration-300"
                style="width: {((currentRatings.engagementRating ?? 0) / 5) * 100}%"
              ></div>
            </div>
          </div>

          <div class="flex flex-col gap-2">
            <div class="flex items-center justify-between">
              <span class="text-sm font-bold text-base-content/70">External Vars</span>
              <span class="text-xl font-display font-black {getRatingColor(currentRatings.externalVariablesRating)}">
                {currentRatings.externalVariablesRating ?? '-'}/5
              </span>
            </div>
            <div class="h-2 bg-base-300 rounded-full overflow-hidden">
              <div
                class="h-full bg-warning transition-all duration-300"
                style="width: {((currentRatings.externalVariablesRating ?? 0) / 5) * 100}%"
              ></div>
            </div>
          </div>

          <div class="flex flex-col gap-2">
            <div class="flex items-center justify-between">
              <span class="text-sm font-bold text-base-content/70">Fatigue</span>
              <span class="text-xl font-display font-black {getRatingColor(currentRatings.accumulatedFatigue)}">
                {currentRatings.accumulatedFatigue ?? '-'}/5
              </span>
            </div>
            <div class="h-2 bg-base-300 rounded-full overflow-hidden">
              <div
                class="h-full bg-error transition-all duration-300"
                style="width: {((currentRatings.accumulatedFatigue ?? 0) / 5) * 100}%"
              ></div>
            </div>
          </div>
        </div>
      </div>
    </button>
  {:else}
    <!-- Display Mode - No Ratings (Add prompt) -->
    <button
      class="card bg-base-100/30 backdrop-blur border border-dashed border-base-content/10 rounded-xl w-full text-left hover:border-primary/30 hover:bg-base-100/50 transition-colors cursor-pointer"
      on:click={startEditing}
      title="Click to add perceived effort ratings"
    >
      <div class="card-body py-4 px-5">
        <div class="flex items-center gap-2">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-base-content/30" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          <span class="text-sm text-base-content/40">Perceived Effort Ratings...</span>
        </div>
      </div>
    </button>
  {/if}
</div>
