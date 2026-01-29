<script lang="ts">
  import type { MovementDTO } from '$lib/api/ApiClient';
  import { addDtSet } from '$lib/stores/sessionStore';
  import { dtSets, setId } from '$lib/utils/training';
  import DTSetItem from './DTSetItem.svelte';

  export let movement: MovementDTO;

  function handleAddDtSet() {
    addDtSet(movement);
  }

  $: sets = dtSets(movement);
</script>

<div class="p-4 rounded-xl bg-base-100 border border-base-content/10">
  <div class="flex items-center justify-between gap-3 mb-3">
    <div class="text-sm font-mono uppercase tracking-widest text-base-content/60">
      Distance / Time Sets
    </div>
    <button
      class="btn btn-sm rounded-xl"
      type="button"
      on:click={handleAddDtSet}
    >
      + 
    </button>
  </div>

  {#if sets.length > 0}
    <div class="space-y-3">
      {#each sets as s, i (setId(s))}
        <DTSetItem {movement} set={s} index={i} />
      {/each}
    </div>
  {/if}
</div>
