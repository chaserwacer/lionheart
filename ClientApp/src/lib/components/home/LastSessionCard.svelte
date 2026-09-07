<script lang="ts">
  import { goto } from "$app/navigation";
  import {
    LIFECYCLE_BADGES,
    LIFECYCLE_LABELS,
    setsOf,
    WEIGHT_UNIT_LABELS,
    type TrainingSessionView,
  } from "$lib/api/training";
  import { relativeDay, training } from "$lib/stores/training.svelte";
  import { groupByMovement } from "$lib/utils/setFormat";

  let { session = null }: { session?: TrainingSessionView | null } = $props();

  // Sets arrive on the session itself now — there is no second fetch for its movements.
  const sets = $derived(session === null ? [] : setsOf(session));
  const groups = $derived(groupByMovement(sets));

  const volume = $derived(
    sets.reduce(
      (total, set) =>
        // Kilograms and pounds are not addable, so only the displayed unit's lifts are counted.
        set.$type === "lift" && set.unit === 1 ? total + set.weight * set.reps : total,
      0,
    ),
  );

  const title = $derived(session?.notes?.trim().slice(0, 30) || "Training session");

  function formatVolume(value: number): string {
    if (value === 0) return "—";
    return value > 1000 ? `${(value / 1000).toFixed(1)}k` : String(Math.round(value));
  }

  function open() {
    if (session !== null) goto(`/training/session/${session.trainingSessionId}`);
  }
</script>

<button
  onclick={open}
  disabled={session === null}
  class="card h-full w-full cursor-pointer border-2 border-base-content/10 bg-base-100 p-6 text-left
         shadow-editorial transition-all duration-200 hover:border-base-content/30 hover:shadow-editorial-lg
         {session === null ? 'cursor-not-allowed opacity-60' : ''}"
>
  <div class="mb-4 flex items-start justify-between">
    <div class="flex flex-col gap-1">
      <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Last</span>
      <h3 class="font-display text-2xl font-black tracking-tight">Session</h3>
    </div>

    {#if session}
      <span class="badge {LIFECYCLE_BADGES[session.$type]} badge-sm">
        {LIFECYCLE_LABELS[session.$type]}
      </span>
    {/if}
  </div>

  {#if session}
    <div class="flex items-start justify-between gap-3">
      <div class="min-w-0">
        <p class="truncate text-xl font-bold uppercase tracking-wide">{title}</p>
        <p class="mt-2 font-mono text-sm uppercase tracking-widest text-base-content/50">
          {relativeDay(session.date)}
        </p>
      </div>
      <span class="shrink-0 text-xl text-base-content/30">&rarr;</span>
    </div>

    <div class="mt-6 grid grid-cols-3 gap-4 border-t-2 border-base-content/10 pt-4">
      <div class="flex flex-col gap-1">
        <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Moves</span>
        <span class="font-display text-2xl font-black">{groups.length}</span>
      </div>
      <div class="flex flex-col gap-1">
        <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Sets</span>
        <span class="font-display text-2xl font-black">{sets.length}</span>
      </div>
      <div class="flex flex-col gap-1">
        <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">
          Volume {WEIGHT_UNIT_LABELS[1]}
        </span>
        <span class="font-display text-2xl font-black">{formatVolume(volume)}</span>
      </div>
    </div>

    {#if groups.length > 0}
      <div class="mt-4 space-y-2">
        {#each groups.slice(0, 4) as group, index (index)}
          <div class="truncate text-sm text-base-content/70">
            • {group.movementDataId === null
              ? "Logged as written"
              : training.movementName(group.movementDataId)}
          </div>
        {/each}
        {#if groups.length > 4}
          <div class="text-xs text-base-content/40">+{groups.length - 4} more</div>
        {/if}
      </div>
    {/if}
  {:else}
    <div class="py-4">
      <p class="text-base-content/50">No completed sessions</p>
      <p class="mt-1 text-xs text-base-content/30">Log one from the Log screen</p>
    </div>
  {/if}
</button>
