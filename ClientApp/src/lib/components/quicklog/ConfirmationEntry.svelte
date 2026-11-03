<script lang="ts">
  import {
    REFERENCE_KIND_LABELS,
    type ReferenceChoice,
    type UnresolvedReference,
  } from "$lib/api/interpretation";

  // Only the names the resolver could not settle are shown. The rest of the entry is already
  // understood and is never re-asked (docs/quick-log.md) — the draft round-trips untouched.
  let {
    references,
    choices,
    onchoose,
  }: {
    references: readonly UnresolvedReference[];
    choices: readonly ReferenceChoice[];
    onchoose: (reference: UnresolvedReference, chosenEntityId: string | null) => void;
  } = $props();

  function answered(reference: UnresolvedReference): ReferenceChoice | undefined {
    return choices.find(
      (choice) => choice.rawName === reference.rawName && choice.kind === reference.kind,
    );
  }

  function confidenceLabel(confidence: number): string {
    return `${Math.round(confidence * 100)}%`;
  }
</script>

<div class="rounded-2xl border-2 border-warning/40 bg-base-100 p-4 shadow-editorial">
  <p class="text-xs font-bold uppercase tracking-widest text-warning">
    {references.length === 1 ? "One name" : `${references.length} names`} to settle
  </p>

  <div class="mt-4 flex flex-col gap-5">
    {#each references as reference (reference.kind + reference.rawName)}
      {@const choice = answered(reference)}
      <div>
        <div class="flex items-baseline gap-2">
          <span class="text-lg font-display font-black tracking-tight">{reference.rawName}</span>
          <span class="text-xs font-mono uppercase tracking-widest text-base-content/40">
            {REFERENCE_KIND_LABELS[reference.kind]}
          </span>
        </div>

        <div class="mt-2 flex flex-wrap gap-2">
          {#each reference.candidates as candidate (candidate.entityId)}
            <button
              type="button"
              onclick={() => onchoose(reference, candidate.entityId)}
              aria-pressed={choice?.chosenEntityId === candidate.entityId}
              class="btn btn-sm min-h-11 h-11 rounded-xl border-2 px-4 normal-case
                     {choice?.chosenEntityId === candidate.entityId
                ? 'border-base-content bg-base-content text-base-100'
                : 'border-base-content/15 bg-base-100'}"
            >
              <span class="font-bold">{candidate.name}</span>
              <span class="ml-2 font-mono text-[0.625rem] opacity-60">
                {confidenceLabel(candidate.confidence)}
              </span>
            </button>
          {/each}

          <button
            type="button"
            onclick={() => onchoose(reference, null)}
            aria-pressed={choice !== undefined && choice.chosenEntityId === null}
            class="btn btn-sm min-h-11 h-11 rounded-xl border-2 border-dashed px-4 normal-case
                   {choice !== undefined && choice.chosenEntityId === null
              ? 'border-base-content bg-base-content text-base-100'
              : 'border-base-content/25 bg-base-100'}"
          >
            Create “{reference.rawName}”
          </button>
        </div>

        {#if reference.candidates.length === 0}
          <p class="mt-2 text-xs text-base-content/40">
            Nothing close in your library — creating it is the only fit.
          </p>
        {/if}
      </div>
    {/each}
  </div>
</div>
