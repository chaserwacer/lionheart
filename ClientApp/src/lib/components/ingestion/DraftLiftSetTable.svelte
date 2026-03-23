<script lang="ts">
  import type { DraftLiftSet } from '$lib/stores/ingestionStore';

  export let sets: DraftLiftSet[];
  export let editable = true;
  export let onUpdate: (sets: DraftLiftSet[]) => void = () => {};

  function updateSet(idx: number, field: keyof DraftLiftSet, value: any) {
    const updated = [...sets];
    updated[idx] = { ...updated[idx], [field]: value };
    onUpdate(updated);
  }

  function removeSet(idx: number) {
    onUpdate(sets.filter((_, i) => i !== idx));
  }
</script>

{#if sets && sets.length > 0}
  <div class="overflow-x-auto">
    <table class="table table-xs">
      <thead>
        <tr class="text-base-content/50">
          <th>#</th>
          <th>Reps</th>
          <th>Weight</th>
          <th>RPE</th>
          <th>Unit</th>
          {#if editable}<th></th>{/if}
        </tr>
      </thead>
      <tbody>
        {#each sets as set, i}
          <tr>
            <td class="text-base-content/40">{i + 1}</td>
            <td>
              {#if editable}
                <input
                  type="number"
                  class="input input-xs input-bordered w-16 rounded"
                  value={set.actualReps}
                  on:change={(e) => updateSet(i, 'actualReps', parseInt(e.currentTarget.value) || 0)}
                />
              {:else}
                {set.actualReps}
              {/if}
            </td>
            <td>
              {#if editable}
                <input
                  type="number"
                  step="0.5"
                  class="input input-xs input-bordered w-20 rounded"
                  value={set.actualWeight}
                  on:change={(e) => updateSet(i, 'actualWeight', parseFloat(e.currentTarget.value) || 0)}
                />
              {:else}
                {set.actualWeight}
              {/if}
            </td>
            <td>
              {#if editable}
                <input
                  type="number"
                  step="0.5"
                  class="input input-xs input-bordered w-16 rounded"
                  value={set.actualRPE}
                  on:change={(e) => updateSet(i, 'actualRPE', parseFloat(e.currentTarget.value) || 0)}
                />
              {:else}
                {set.actualRPE || '-'}
              {/if}
            </td>
            <td>
              <span class="text-xs text-base-content/50">{set.weightUnit}</span>
            </td>
            {#if editable}
              <td>
                <button
                  class="btn btn-ghost btn-xs text-error"
                  on:click={() => removeSet(i)}
                >
                  &times;
                </button>
              </td>
            {/if}
          </tr>
        {/each}
      </tbody>
    </table>
  </div>
{/if}
