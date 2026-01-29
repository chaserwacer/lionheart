<script lang="ts">
  import type { MovementDTO } from '$lib/api/ApiClient';
  import {
    isEditing,
    isLoading,
    displayWeightUnit,
    updateLiftSet,
    deleteLiftSet,
    addLiftSet,
    addDtSet,
  } from '$lib/stores/sessionStore';
  import { liftSets, setId, setKindFor } from '$lib/utils/training';
  import { weightUnitLabel, displayWeight, parseNumberOrZero } from '$lib/utils/training';

  export let movement: MovementDTO;

  $: sets = liftSets(movement);

  // Helper to check if a set has recommendations hidden (all rec values are null)
  function isSetRecHidden(s: any): boolean {
    return s.recommendedReps == null && s.recommendedWeight == null && s.recommendedRPE == null;
  }

  // Toggle hide rec for a specific set
  async function handleSetToggleChange(s: any, hideRec: boolean) {
    if (hideRec) {
      // Hiding recommended - set all to null/undefined
      await updateLiftSet(movement, s, {
        recommendedReps: undefined,
        recommendedWeight: undefined,
        recommendedRPE: undefined,
      });
    } else {
      // Showing recommended - set defaults from actuals
      await updateLiftSet(movement, s, {
        recommendedReps: s.actualReps ?? 0,
        recommendedWeight: s.actualWeight ?? 0,
        recommendedRPE: s.actualRPE ?? 0,
      });
    }
  }

  function handleUpdateActuals(s: any, field: string, value: string) {
    const patch: any = { [field]: parseNumberOrZero(value) };
    if (field === 'actualWeight') {
      patch.weightUnit = $displayWeightUnit;
    }
    updateLiftSet(movement, s, patch);
  }

  function handleUpdateSet(s: any, field: string, value: string) {
    const patch: any = { [field]: parseNumberOrZero(value) };
    if (field === 'actualWeight') {
      patch.weightUnit = $displayWeightUnit;
    }
    updateLiftSet(movement, s, patch);
  }

  function handleDeleteSet(s: any) {
    if (!confirm('Delete this set?')) return;
    deleteLiftSet(movement, s);
  }

  function handleAddLiftSet() {
    // New sets default to hidden recommendations
    addLiftSet(movement, true);
  }

  function handleAddDtSet() {
    addDtSet(movement);
  }

  function copyRecommendedToActual(s: any, field: 'reps' | 'weight' | 'rpe') {
    const fieldMap = {
      reps: { from: 'recommendedReps', to: 'actualReps' },
      weight: { from: 'recommendedWeight', to: 'actualWeight' },
      rpe: { from: 'recommendedRPE', to: 'actualRPE' },
    };
    const { from, to } = fieldMap[field];
    if (s[from] != null) {
      handleUpdateActuals(s, to, String(s[from]));
    }
  }

  $: kind = setKindFor(movement);

  // Check if any set has visible recommendations
  $: anyRecVisible = sets.some((s: any) => !isSetRecHidden(s));
</script>

<div class="p-4 rounded-xl bg-base-100 border border-base-content/10">
  <div class="flex items-center justify-between gap-3 mb-3">
    <div class="flex items-center gap-3">
      <div class="text-sm font-mono uppercase tracking-widest text-base-content/60">
        Lift Sets
      </div>
    </div>
    <div class="flex gap-2">
      {#if kind === 'none'}
        <button class="btn btn-xs btn-outline" on:click={handleAddLiftSet}>+</button>
      {:else if kind === 'lift'}
        <button class="btn btn-xs btn-outline" on:click={handleAddLiftSet}>+</button>
      {:else}
        <button class="btn btn-xs btn-outline" on:click={handleAddDtSet}>+</button>
      {/if}
    </div>
  </div>

  {#if sets.length != 0}
    <div class="overflow-x-auto">
      <table class="table table-sm">
        <thead>
          <tr class="text-xs font-mono uppercase tracking-widest text-base-content/50">
            <th>#</th>
            {#if anyRecVisible}
              <th>Rec Reps</th>
              <th>Rec Wt</th>
              <th>Rec RPE</th>
            {/if}
            <th>Reps</th>
            <th>Wt </th>
            <th>RPE</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {#each sets as s, i (setId(s))}
            {@const hideRec = isSetRecHidden(s)}
            <tr>
              <td class="font-mono">{i + 1}</td>
              {#if anyRecVisible}
                {#if hideRec}
                  <td>—</td>
                  <td>—</td>
                  <td>—</td>
                {:else}
                  <td>{s.recommendedReps ?? '—'}</td>
                  <td>
                    {#if s.recommendedWeight == null}
                      —
                    {:else}
                      {displayWeight(s.recommendedWeight, s.weightUnit, $displayWeightUnit)}
                    {/if}
                  </td>
                  <td>{s.recommendedRPE ?? '—'}</td>
                {/if}
              {/if}

              {#if !$isEditing}
                <td class="relative group">
                  {#if !hideRec}
                    <button
                      class="absolute -left-1 top-1/2 -translate-y-1/2 opacity-0 group-hover:opacity-100 transition-opacity duration-200 text-xs text-primary hover:text-primary-focus"
                      on:click={() => copyRecommendedToActual(s, 'reps')}
                      title="Copy from recommended"
                    >
                      ←
                    </button>
                  {/if}
                  <input
                    type="number"
                    class="input input-xs input-bordered w-16 font-semibold"
                    value={s.actualReps}
                    on:change={(e) => handleUpdateActuals(s, 'actualReps', e.currentTarget.value)}
                  />
                </td>
                <td class="relative group">
                  {#if !hideRec}
                    <button
                      class="absolute -left-1 top-1/2 -translate-y-1/2 opacity-0 group-hover:opacity-100 transition-opacity duration-200 text-xs text-primary hover:text-primary-focus"
                      on:click={() => copyRecommendedToActual(s, 'weight')}
                      title="Copy from recommended"
                    >
                      ←
                    </button>
                  {/if}
                  <input
                    type="number"
                    class="input input-xs input-bordered w-20 font-semibold"
                    value={s.actualWeight}
                    on:change={(e) => handleUpdateActuals(s, 'actualWeight', e.currentTarget.value)}
                  />
                </td>
                <td class="relative group">
                  {#if !hideRec}
                    <button
                      class="absolute -left-1 top-1/2 -translate-y-1/2 opacity-0 group-hover:opacity-100 transition-opacity duration-200 text-xs text-primary hover:text-primary-focus"
                      on:click={() => copyRecommendedToActual(s, 'rpe')}
                      title="Copy from recommended"
                    >
                      ←
                    </button>
                  {/if}
                  <input
                    type="number"
                    step="0.5"
                    class="input input-xs input-bordered w-16 font-semibold"
                    value={s.actualRPE}
                    on:change={(e) => handleUpdateActuals(s, 'actualRPE', e.currentTarget.value)}
                  />
                </td>
                <td class="text-right flex gap-3 justify-end">
                  <button
                    class="btn btn-xs btn-ghost"
                    type="button"
                    on:click={() => handleSetToggleChange(s, !hideRec)}
                    title={hideRec ? 'Show recommendations' : 'Hide recommendations'}
                  >
                    {#if hideRec}▼{:else}▲{/if}
                  </button>
                  <button
                    class="btn btn-xs btn-outline btn-error"
                    type="button"
                    on:click={() => handleDeleteSet(s)}
                  >
                    X
                  </button>
                </td>
              {:else}
                <td>
                  <input
                    class="input input-sm input-bordered w-20"
                    type="number"
                    value={s.actualReps}
                    on:change={(e) => handleUpdateSet(s, 'actualReps', e.currentTarget.value)}
                  />
                </td>
                <td>
                  <input
                    class="input input-sm input-bordered w-28"
                    type="number"
                    value={s.actualWeight}
                    on:change={(e) => handleUpdateSet(s, 'actualWeight', e.currentTarget.value)}
                  />
                </td>
                <td>
                  <input
                    class="input input-sm input-bordered w-24"
                    type="number"
                    step="0.5"
                    value={s.actualRPE}
                    on:change={(e) => handleUpdateSet(s, 'actualRPE', e.currentTarget.value)}
                  />
                </td>
                <td class="text-right flex gap-3 justify-end">
                  <button
                    class="btn btn-xs btn-ghost"
                    type="button"
                    on:click={() => handleSetToggleChange(s, !hideRec)}
                    title={hideRec ? 'Show recommendations' : 'Hide recommendations'}
                  >
                    {#if hideRec}▼{:else}▲{/if}
                  </button>
                  <button
                    class="btn btn-xs btn-outline btn-error"
                    type="button"
                    on:click={() => handleDeleteSet(s)}
                  >
                    Delete
                  </button>
                </td>
              {/if}
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {/if}
</div>
