<script lang="ts">
  import type { MovementDTO } from '$lib/api/ApiClient';
  import { isEditing, updateDtSet, deleteDtSet, addDtSet } from '$lib/stores/sessionStore';
  import { dtSets, setId } from '$lib/utils/training';
  import { formatTimeSpan, parseNumberOrZero } from '$lib/utils/training';

  export let movement: MovementDTO;

  $: sets = dtSets(movement);

  // Responsive view mode for narrow screens: 'actual' or 'recommended'
  let narrowViewMode: 'actual' | 'recommended' = 'actual';

  // Check if any set has visible recommendations
  function hasAnyRec(s: any): boolean {
    return (
      s.recommendedDistance != null ||
      s.recommendedDuration != null ||
      s.targetPace != null ||
      s.recommendedRest != null
    );
  }

  $: anyRecVisible = sets.some((s: any) => hasAnyRec(s));

  function handleUpdate(s: any, field: string, value: string) {
    if (field === 'actualDistance' || field === 'recommendedDistance' || field === 'actualRPE') {
      updateDtSet(movement, s, { [field]: parseNumberOrZero(value) });
    } else {
      updateDtSet(movement, s, { [field]: value });
    }
  }

  function handleDelete(s: any) {
    if (!confirm('Delete this set?')) return;
    deleteDtSet(movement, s);
  }

  function handleAddDtSet() {
    addDtSet(movement);
  }

  function copyRecToActual(s: any, recField: string, actField: string) {
    if (s[recField] != null) {
      handleUpdate(s, actField, String(s[recField]));
    }
  }
</script>

<div class="p-4 rounded-xl bg-base-100 border border-base-content/10">
  <div class="flex items-center justify-between gap-3 mb-3">
    <div class="flex items-center gap-3">
      <div class="text-sm font-mono uppercase tracking-widest text-base-content/60">
        Distance / Time Sets
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
    <button class="btn btn-xs btn-outline" type="button" on:click={handleAddDtSet}>
      +
    </button>
  </div>

  {#if sets.length > 0}
    <div class="overflow-x-auto md:overflow-x-visible">
      <table class="table table-sm">
        <thead>
          <tr class="text-xs font-mono uppercase tracking-widest text-base-content/50">
            <th>#</th>
            {#if anyRecVisible}
              <!-- Recommended columns: always show on md+, toggle on narrow -->
              <th class="hidden md:table-cell {narrowViewMode === 'recommended' ? '!table-cell' : ''}">Rec Dist</th>
              <th class="hidden md:table-cell {narrowViewMode === 'recommended' ? '!table-cell' : ''}">Rec Dur</th>
              <th class="hidden md:table-cell {narrowViewMode === 'recommended' ? '!table-cell' : ''}">Target Pace</th>
            {/if}
            <!-- Actual columns: always show on md+, toggle on narrow -->
            <th class="hidden md:table-cell {narrowViewMode === 'actual' || !anyRecVisible ? '!table-cell' : ''}">Distance</th>
            <th class="hidden md:table-cell {narrowViewMode === 'actual' || !anyRecVisible ? '!table-cell' : ''}">Duration</th>
            <th class="hidden md:table-cell {narrowViewMode === 'actual' || !anyRecVisible ? '!table-cell' : ''}">Pace</th>
            <th class="hidden md:table-cell {narrowViewMode === 'actual' || !anyRecVisible ? '!table-cell' : ''}">RPE</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {#each sets as s, i (setId(s))}
            {@const showRec = hasAnyRec(s)}
            {@const recCellClass = `hidden md:table-cell ${narrowViewMode === 'recommended' ? '!table-cell' : ''}`}
            {@const actCellClass = `hidden md:table-cell ${narrowViewMode === 'actual' || !anyRecVisible ? '!table-cell' : ''}`}
            <tr>
              <td class="font-mono">{i + 1}</td>

              {#if anyRecVisible}
                {#if !showRec}
                  <td class={recCellClass}>—</td>
                  <td class={recCellClass}>—</td>
                  <td class={recCellClass}>—</td>
                {:else if $isEditing}
                  <td class={recCellClass}>
                    <input
                      class="input input-sm input-bordered w-20"
                      type="number"
                      value={s.recommendedDistance ?? ''}
                      on:change={(e) => handleUpdate(s, 'recommendedDistance', e.currentTarget.value)}
                    />
                  </td>
                  <td class={recCellClass}>
                    <input
                      class="input input-sm input-bordered w-24"
                      value={formatTimeSpan(s.recommendedDuration) || ''}
                      placeholder="hh:mm:ss"
                      on:change={(e) => handleUpdate(s, 'recommendedDuration', e.currentTarget.value)}
                    />
                  </td>
                  <td class={recCellClass}>
                    <input
                      class="input input-sm input-bordered w-24"
                      value={formatTimeSpan(s.targetPace) || ''}
                      placeholder="mm:ss"
                      on:change={(e) => handleUpdate(s, 'targetPace', e.currentTarget.value)}
                    />
                  </td>
                {:else}
                  <td class={recCellClass}>{s.recommendedDistance ?? '—'}</td>
                  <td class={recCellClass}>{formatTimeSpan(s.recommendedDuration) || '—'}</td>
                  <td class={recCellClass}>{formatTimeSpan(s.targetPace) || '—'}</td>
                {/if}
              {/if}

              {#if !$isEditing}
                <td class="relative group {actCellClass}">
                  {#if showRec && s.recommendedDistance != null}
                    <button
                      class="absolute -left-1 top-1/2 -translate-y-1/2 opacity-0 group-hover:opacity-100 transition-opacity duration-200 text-xs text-primary hover:text-primary-focus"
                      on:click={() => copyRecToActual(s, 'recommendedDistance', 'actualDistance')}
                      title="Copy from recommended"
                    >
                      ←
                    </button>
                  {/if}
                  <input
                    type="number"
                    class="input input-xs input-bordered w-16 font-semibold"
                    value={s.actualDistance}
                    on:change={(e) => handleUpdate(s, 'actualDistance', e.currentTarget.value)}
                  />
                </td>
                <td class="relative group {actCellClass}">
                  {#if showRec && s.recommendedDuration != null}
                    <button
                      class="absolute -left-1 top-1/2 -translate-y-1/2 opacity-0 group-hover:opacity-100 transition-opacity duration-200 text-xs text-primary hover:text-primary-focus"
                      on:click={() => copyRecToActual(s, 'recommendedDuration', 'actualDuration')}
                      title="Copy from recommended"
                    >
                      ←
                    </button>
                  {/if}
                  <input
                    class="input input-xs input-bordered w-20 font-semibold"
                    value={formatTimeSpan(s.actualDuration) || ''}
                    placeholder="hh:mm:ss"
                    on:change={(e) => handleUpdate(s, 'actualDuration', e.currentTarget.value)}
                  />
                </td>
                <td class="relative group {actCellClass}">
                  {#if showRec && s.targetPace != null}
                    <button
                      class="absolute -left-1 top-1/2 -translate-y-1/2 opacity-0 group-hover:opacity-100 transition-opacity duration-200 text-xs text-primary hover:text-primary-focus"
                      on:click={() => copyRecToActual(s, 'targetPace', 'actualPace')}
                      title="Copy from recommended"
                    >
                      ←
                    </button>
                  {/if}
                  <input
                    class="input input-xs input-bordered w-20 font-semibold"
                    value={formatTimeSpan(s.actualPace) || ''}
                    placeholder="mm:ss"
                    on:change={(e) => handleUpdate(s, 'actualPace', e.currentTarget.value)}
                  />
                </td>
                <td class={actCellClass}>
                  <input
                    type="number"
                    step="0.5"
                    class="input input-xs input-bordered w-14 font-semibold"
                    value={s.actualRPE}
                    on:change={(e) => handleUpdate(s, 'actualRPE', e.currentTarget.value)}
                  />
                </td>
                <td class="text-right">
                  <button
                    class="btn btn-xs btn-outline btn-error"
                    type="button"
                    on:click={() => handleDelete(s)}
                  >
                    X
                  </button>
                </td>
              {:else}
                <td class={actCellClass}>
                  <input
                    class="input input-sm input-bordered w-20"
                    type="number"
                    value={s.actualDistance}
                    on:change={(e) => handleUpdate(s, 'actualDistance', e.currentTarget.value)}
                  />
                </td>
                <td class={actCellClass}>
                  <input
                    class="input input-sm input-bordered w-24"
                    value={formatTimeSpan(s.actualDuration) || ''}
                    placeholder="hh:mm:ss"
                    on:change={(e) => handleUpdate(s, 'actualDuration', e.currentTarget.value)}
                  />
                </td>
                <td class={actCellClass}>
                  <input
                    class="input input-sm input-bordered w-24"
                    value={formatTimeSpan(s.actualPace) || ''}
                    placeholder="mm:ss"
                    on:change={(e) => handleUpdate(s, 'actualPace', e.currentTarget.value)}
                  />
                </td>
                <td class={actCellClass}>
                  <input
                    class="input input-sm input-bordered w-20"
                    type="number"
                    step="0.5"
                    value={s.actualRPE}
                    on:change={(e) => handleUpdate(s, 'actualRPE', e.currentTarget.value)}
                  />
                </td>
                <td class="text-right">
                  <button
                    class="btn btn-xs btn-outline btn-error"
                    type="button"
                    on:click={() => handleDelete(s)}
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
