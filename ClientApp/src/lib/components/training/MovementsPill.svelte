<script lang="ts">
  export let sessionId: string | undefined;

  // Parent supplies these so this component stays dumb + reusable
  export let getCount: (sessionId: string) => number;
  export let getLimited: (
    sessionId: string,
    max?: number,
  ) => { shown: string[]; extra: number };

  export let maxShown = 4;
  export let showArrow = true;

  $: mc = sessionId ? getCount(sessionId) : 0;
  $: lim = sessionId ? getLimited(sessionId, maxShown) : { shown: [], extra: 0 };
</script>

<div class="flex items-center gap-3 shrink-0 w-full sm:w-auto sm:ml-auto">
  <div
    class="px-3 py-2 rounded-xl bg-base-100 border border-base-content/10 text-center w-full sm:w-auto"
  >
    <div class="text-lg font-display font-black leading-none">
      {mc} Movements
    </div>

    {#if mc > 0}
      <div class="mt-2 flex flex-wrap justify-center gap-2 max-w-full">
        {#each lim.shown as n, i (i)}
          <span class="badge badge-outline font-mono text-xs whitespace-nowrap">
            {n}
          </span>
        {/each}

        {#if lim.extra > 0}
          <span class="badge badge-ghost font-mono text-xs whitespace-nowrap">
            +{lim.extra} more
          </span>
        {/if}
      </div>
    {/if}
  </div>

  {#if showArrow}
    <span class="opacity-50 text-sm">→</span>
  {/if}
</div>
