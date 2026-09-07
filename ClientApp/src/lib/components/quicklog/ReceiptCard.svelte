<script lang="ts">
  import { setsOf, type TrainingSessionView } from "$lib/api/training";
  import type { Receipt } from "$lib/stores/quickLog.svelte";
  import { describeSet, facetLabel } from "$lib/utils/setFormat";

  // A correct parse is not confirmed, only shown — one Undo is the whole affordance
  // (docs/quick-log.md).
  let {
    receipt,
    movementNames,
    onundo,
    ondismiss,
  }: {
    receipt: Receipt;
    movementNames: Record<string, string>;
    onundo: () => void;
    ondismiss: () => void;
  } = $props();

  const nameOf = (id: string) => movementNames[id] ?? "Movement";

  function facetsOf(session: TrainingSessionView) {
    return session.$type === "completed" && session.decoration.$type === "decorated"
      ? session.decoration.facets
      : [];
  }
</script>

<div
  class="rounded-2xl border-2 p-4 shadow-editorial
         {receipt.undone ? 'border-base-content/10 bg-base-200' : 'border-success/40 bg-base-100'}"
>
  <div class="flex items-start justify-between gap-3">
    <div class="min-w-0">
      <p
        class="text-xs font-bold uppercase tracking-widest
               {receipt.undone ? 'text-base-content/40' : 'text-success'}"
      >
        {receipt.undone ? "Undone" : "Logged"}
      </p>
      <p class="mt-1 text-sm italic text-base-content/60">“{receipt.rawText}”</p>
      {#if receipt.undoProblem}
        <p class="mt-1 text-xs text-error">Undo failed: {receipt.undoProblem}</p>
      {/if}
    </div>

    {#if receipt.undone}
      <button
        type="button"
        onclick={ondismiss}
        class="btn btn-ghost btn-sm min-h-11 h-11 shrink-0 text-xs font-bold uppercase tracking-widest"
      >
        Dismiss
      </button>
    {:else}
      <button
        type="button"
        onclick={onundo}
        class="btn btn-outline btn-sm min-h-11 h-11 shrink-0 rounded-xl border-2 text-xs font-bold uppercase tracking-widest"
      >
        Undo
      </button>
    {/if}
  </div>

  {#if !receipt.undone}
    {#each receipt.sessions as session (session.trainingSessionId)}
      {@const sets = setsOf(session)}
      <div class="mt-4 border-t-2 border-base-content/10 pt-3">
        {#if sets.length === 0}
          <p class="text-sm text-base-content/50">Logged with no sets.</p>
        {/if}

        {#each sets as set, index (index)}
          <p class="text-sm text-base-content/80">• {describeSet(set, nameOf)}</p>
        {/each}

        {#if session.notes}
          <p class="mt-2 text-xs text-base-content/50">{session.notes}</p>
        {/if}

        {#if facetsOf(session).length > 0}
          <div class="mt-3 flex flex-wrap gap-1.5">
            {#each facetsOf(session) as facet, index (index)}
              <span class="badge badge-outline badge-sm font-mono text-[0.625rem]">
                {facetLabel(facet)}
              </span>
            {/each}
          </div>
        {/if}
      </div>
    {/each}

    {#if receipt.contextIds.length > 0}
      <p class="mt-3 text-xs text-base-content/50">
        Plus {receipt.contextIds.length}
        {receipt.contextIds.length === 1 ? "context entry" : "context entries"}.
      </p>
    {/if}

    {#if receipt.warnings.length > 0}
      <ul class="mt-3 space-y-1">
        {#each receipt.warnings as warning, index (index)}
          <li class="text-xs text-warning">{warning}</li>
        {/each}
      </ul>
    {/if}
  {/if}
</div>
