<script lang="ts">
  import { goto } from "$app/navigation";
  import {
    LIFECYCLE_BADGES,
    LIFECYCLE_LABELS,
    prescriptionsOf,
    type TrainingSessionView,
  } from "$lib/api/training";
  import { relativeDay, training } from "$lib/stores/training.svelte";
  import { describePrescription } from "$lib/utils/setFormat";

  let { session = null }: { session?: TrainingSessionView | null } = $props();

  // A planned session carries targets, not performed sets — the two never half-fill one row.
  const prescriptions = $derived(session === null ? [] : prescriptionsOf(session));

  const title = $derived(session?.notes?.trim().slice(0, 30) || "Training session");

  const nameOf = (id: string) => training.movementName(id);

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
      <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Next</span>
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
          {relativeDay(session.$type === "planned" ? session.plannedFor : session.date)}
        </p>
      </div>
      <span class="shrink-0 text-xl text-base-content/30">&rarr;</span>
    </div>

    <div class="mt-6 border-t-2 border-base-content/10 pt-4">
      <div class="flex items-baseline gap-2">
        <span class="font-display text-4xl font-black">{prescriptions.length}</span>
        <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">
          Sets prescribed
        </span>
      </div>

      {#if prescriptions.length > 0}
        <div class="mt-4 space-y-2">
          {#each prescriptions.slice(0, 4) as prescription, index (index)}
            <div class="truncate text-sm text-base-content/70">
              • {describePrescription(prescription, nameOf)}
            </div>
          {/each}
          {#if prescriptions.length > 4}
            <div class="text-xs text-base-content/40">+{prescriptions.length - 4} more</div>
          {/if}
        </div>
      {/if}
    </div>
  {:else}
    <div class="py-4">
      <p class="text-base-content/50">Nothing planned</p>
      <p class="mt-1 text-xs text-base-content/30">Plan one from Training</p>
    </div>
  {/if}
</button>
