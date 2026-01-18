<script lang="ts">
  import type { ActivityDTO } from "$lib/api/ApiClient";

  export let activities: ActivityDTO[] = [];

  let modalOpen = false;

  $: lastActivity =
    activities.length > 0 ? activities[activities.length - 1] : null;

  function openModal() {
    if (lastActivity) modalOpen = true;
  }

  function closeModal() {
    modalOpen = false;
  }

 
  function getDifficultyLabel(rating: number): string {
    if (rating >= 4.5) return "Extreme";
    if (rating >= 4) return "Hard";
    if (rating >= 3) return "Moderate";
    if (rating >= 2) return "Easy";
    return "Light";
  }

  function getDifficultyColor(rating: number): string {
    if (rating >= 4) return "badge-error";
    if (rating >= 3) return "badge-warning";
    return "badge-success";
  }

  $: difficultyRating =
    lastActivity?.perceivedEffortRatings?.difficultyRating ?? 0;
  $: engagementRating =
    lastActivity?.perceivedEffortRatings?.engagementRating ?? 0;
  $: externalVariablesRating =
    lastActivity?.perceivedEffortRatings?.externalVariablesRating ?? 0;
</script>

<!-- Card -->
<button
  on:click={openModal}
  class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
         cursor-pointer p-6 text-left w-full h-full
         border-2 border-base-content/10 hover:border-base-content/30
         {!lastActivity ? 'opacity-60' : ''}"
  disabled={!lastActivity}
>
  <div class="flex items-start justify-between mb-6">
    <div class="flex flex-col gap-1">
      <span
        class="text-xs font-bold uppercase tracking-widest text-base-content/50"
        >Recent</span
      >
      <h3 class="text-2xl font-display font-black tracking-tight">Activity</h3>
    </div>
    {#if lastActivity}
      <svg
        xmlns="http://www.w3.org/2000/svg"
        fill="none"
        viewBox="0 0 24 24"
        stroke-width="2"
        stroke="currentColor"
        class="w-6 h-6 text-base-content/30"
      >
        <path
          stroke-linecap="round"
          stroke-linejoin="round"
          d="m8.25 4.5 7.5 7.5-7.5 7.5"
        />
      </svg>
    {/if}
  </div>

  {#if lastActivity}
    <div>
      <p class="text-xl font-bold uppercase tracking-wide truncate">
        {lastActivity.name }
      </p>
      
    </div>

    <div
      class="mt-6 pt-4 border-t-2 border-base-content/10 grid grid-cols-2 gap-4"
    >
      <div class="flex flex-col gap-1">
        <span
          class="text-xs font-bold uppercase tracking-wider text-base-content/50"
          >Time</span
        >
        <span class="text-3xl font-display font-black"
          >{lastActivity.timeInMinutes}</span
        >
      </div>
      <div class="flex flex-col gap-1">
        <span
          class="text-xs font-bold uppercase tracking-wider text-base-content/50"
          >Level</span
        >
        <span
          class="text-lg font-bold uppercase tracking-wide {getDifficultyColor(
            difficultyRating,
          ).replace('badge-', 'text-')}"
        >
          {getDifficultyLabel(difficultyRating)}
        </span>
      </div>
    </div>
  {:else}
    <div class="text-center py-4">
      <p class="text-base-content/50">No recent activity</p>
      <p class="text-xs text-base-content/30 mt-1">Log an activity</p>
    </div>
  {/if}
</button>

<!-- Modal -->
<dialog class="modal" class:modal-open={modalOpen}>
  <div
    class="modal-box rounded-3xl max-w-2xl bg-base-100 p-0 overflow-hidden max-h-[90vh]"
  >
    <!-- Header -->
    <div class="p-6 pb-4 border-b border-base-200">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div
            class="w-12 h-12 rounded-2xl bg-warning/10 flex items-center justify-center"
          >
            <svg
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              stroke-width="1.5"
              stroke="currentColor"
              class="w-6 h-6 text-warning"
            >
              <path
                stroke-linecap="round"
                stroke-linejoin="round"
                d="M3.75 13.5l10.5-11.25L12 10.5h8.25L9.75 21.75 12 13.5H3.75z"
              />
            </svg>
          </div>
          <div>
            <h3 class="font-semibold text-lg">
              {lastActivity?.name || "Activity"}
            </h3>
            
          </div>
        </div>
        <button on:click={closeModal} class="btn btn-ghost btn-sm btn-circle">
          <svg
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
            stroke-width="1.5"
            stroke="currentColor"
            class="w-5 h-5"
          >
            <path
              stroke-linecap="round"
              stroke-linejoin="round"
              d="M6 18 18 6M6 6l12 12"
            />
          </svg>
        </button>
      </div>
    </div>

    <!-- Content -->
    <div class="p-6 overflow-y-auto max-h-[60vh]">
      {#if lastActivity}
        <!-- Summary Stats -->
        <div class="grid grid-cols-3 gap-4 mb-6">
          <div class="bg-base-200 rounded-2xl p-4 text-center">
            <p class="text-2xl font-bold">{lastActivity.timeInMinutes}</p>
            <p class="text-xs text-base-content/50">Minutes</p>
          </div>
          <div class="bg-base-200 rounded-2xl p-4 text-center">
            <p class="text-2xl font-bold">
              {lastActivity.caloriesBurned || "--"}
            </p>
            <p class="text-xs text-base-content/50">Calories</p>
          </div>
          <div class="bg-base-200 rounded-2xl p-4 text-center">
            <p class="text-2xl font-bold">{difficultyRating}/5</p>
            <p class="text-xs text-base-content/50">Difficulty</p>
          </div>
        </div>

        <!-- Ratings -->
        <h4 class="font-medium mb-3">Ratings</h4>
        <div class="space-y-4 mb-6">
          <div class="flex items-center justify-between">
            <span class="text-sm">Difficulty</span>
            <div class="flex items-center gap-3 w-1/2">
              <progress
                class="progress progress-warning flex-1 h-2"
                value={difficultyRating}
                max="5"
              ></progress>
              <span class="text-sm font-medium w-8 text-right"
                >{difficultyRating}</span
              >
            </div>
          </div>
          <div class="flex items-center justify-between">
            <span class="text-sm">Engagement</span>
            <div class="flex items-center gap-3 w-1/2">
              <progress
                class="progress progress-info flex-1 h-2"
                value={engagementRating}
                max="5"
              ></progress>
              <span class="text-sm font-medium w-8 text-right"
                >{engagementRating}</span
              >
            </div>
          </div>
          <div class="flex items-center justify-between">
            <span class="text-sm">External Variables</span>
            <div class="flex items-center gap-3 w-1/2">
              <progress
                class="progress progress-success flex-1 h-2"
                value={externalVariablesRating}
                max="5"
              ></progress>
              <span class="text-sm font-medium w-8 text-right"
                >{externalVariablesRating}</span
              >
            </div>
          </div>
        </div>

        
      {/if}
    </div>
  </div>
  <form method="dialog" class="modal-backdrop bg-black/50 backdrop-blur-sm">
    <button on:click={closeModal}>close</button>
  </form>
</dialog>
