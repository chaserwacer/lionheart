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

  // Responsive view mode for narrow screens: 'actual' or 'recommended'
  let narrowViewMode: 'actual' | 'recommended' = 'actual';

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

  function handleUpdateRecommended(s: any, field: string, value: string) {
    const patch: any = { [field]: parseNumberOrZero(value) };
    if (field === 'recommendedWeight') {
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
      {#if anyRecVisible}
        <label class="md:hidden flex items-center gap-2 ml-4 cursor-pointer">
          <span class="text-xs text-base-content/50">{narrowViewMode === 'recommended' ? 'Rec' : 'Actual'}</span>
          <input
            type="checkbox"
            class="toggle toggle-xs toggle-primary"
            checked={narrowViewMode === 'actual'}
            on:change={() => (narrowViewMode = narrowViewMode === 'actual' ? 'recommended' : 'actual')}
          />
        </label>
      {/if}
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
    <div class="overflow-x-auto md:overflow-x-visible">
      <table class="table table-sm">
        <thead>
          <tr class="text-xs font-mono uppercase tracking-widest text-base-content/50">
            <th>#</th>
            {#if anyRecVisible}
              <!-- Recommended columns: always show on md+, toggle on narrow -->
              <th class="hidden md:table-cell {narrowViewMode === 'recommended' ? '!table-cell' : ''}">Rec Reps</th>
              <th class="hidden md:table-cell {narrowViewMode === 'recommended' ? '!table-cell' : ''}">Rec Wt</th>
              <th class="hidden md:table-cell {narrowViewMode === 'recommended' ? '!table-cell' : ''}">Rec RPE</th>
            {/if}
            <!-- Actual columns: always show on md+, toggle on narrow -->
            <th class="hidden md:table-cell {narrowViewMode === 'actual' || !anyRecVisible ? '!table-cell' : ''}">Reps</th>
            <th class="hidden md:table-cell {narrowViewMode === 'actual' || !anyRecVisible ? '!table-cell' : ''}">Wt</th>
            <th class="hidden md:table-cell {narrowViewMode === 'actual' || !anyRecVisible ? '!table-cell' : ''}">RPE</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {#each sets as s, i (setId(s))}
            {@const hideRec = isSetRecHidden(s)}
            {@const recCellClass = `hidden md:table-cell ${narrowViewMode === 'recommended' ? '!table-cell' : ''}`}
            {@const actCellClass = `hidden md:table-cell ${narrowViewMode === 'actual' || !anyRecVisible ? '!table-cell' : ''}`}
            <tr>
              <td class="font-mono">{i + 1}</td>
              {#if anyRecVisible}
                {#if hideRec}
                  <td class={recCellClass}>—</td>
                  <td class={recCellClass}>—</td>
                  <td class={recCellClass}>—</td>
                {:else if $isEditing}
                  <td class={recCellClass}>
                    <input
                      class="input input-sm input-bordered w-20"
                      type="number"
                      value={s.recommendedReps ?? ''}
                      on:change={(e) => handleUpdateRecommended(s, 'recommendedReps', e.currentTarget.value)}
                    />
                  </td>
                  <td class={recCellClass}>
                    <input
                      class="input input-sm input-bordered w-28"
                      type="number"
                      value={s.recommendedWeight ?? ''}
                      on:change={(e) => handleUpdateRecommended(s, 'recommendedWeight', e.currentTarget.value)}
                    />
                  </td>
                  <td class={recCellClass}>
                    <input
                      class="input input-sm input-bordered w-24"
                      type="number"
                      step="0.5"
                      value={s.recommendedRPE ?? ''}
                      on:change={(e) => handleUpdateRecommended(s, 'recommendedRPE', e.currentTarget.value)}
                    />
                  </td>
                {:else}
                  <td class={recCellClass}>{s.recommendedReps ?? '—'}</td>
                  <td class={recCellClass}>
                    {#if s.recommendedWeight == null}
                      —
                    {:else}
                      {displayWeight(s.recommendedWeight, s.weightUnit, $displayWeightUnit)}
                    {/if}
                  </td>
                  <td class={recCellClass}>{s.recommendedRPE ?? '—'}</td>
                {/if}
              {/if}

              {#if !$isEditing}
                <td class="relative group {actCellClass}">
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
                <td class="relative group {actCellClass}">
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
                <td class="relative group {actCellClass}">
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
                <td class={actCellClass}>
                  <input
                    class="input input-sm input-bordered w-20"
                    type="number"
                    value={s.actualReps}
                    on:change={(e) => handleUpdateSet(s, 'actualReps', e.currentTarget.value)}
                  />
                </td>
                <td class={actCellClass}>
                  <input
                    class="input input-sm input-bordered w-28"
                    type="number"
                    value={s.actualWeight}
                    on:change={(e) => handleUpdateSet(s, 'actualWeight', e.currentTarget.value)}
                  />
                </td>
                <td class={actCellClass}>
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
