<script lang="ts">
  import type { MovementDTO } from '$lib/api/ApiClient';
  import { isEditing, updateDtSet, deleteDtSet, addDtSet } from '$lib/stores/sessionStore';
  import { dtSets, setId } from '$lib/utils/training';
  import { formatTimeSpan, parseNumberOrZero } from '$lib/utils/training';

  export let movement: MovementDTO;

  $: sets = dtSets(movement);

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
    <div class="text-sm font-mono uppercase tracking-widest text-base-content/60">
      Distance / Time Sets
    </div>
    <button class="btn btn-xs btn-outline" type="button" on:click={handleAddDtSet}>
      +
    </button>
  </div>

  {#if sets.length > 0}
    <div class="overflow-x-auto">
      <table class="table table-sm">
        <thead>
          <tr class="text-xs font-mono uppercase tracking-widest text-base-content/50">
            <th>#</th>
            {#if anyRecVisible}
              <th>Rec Dist</th>
              <th>Rec Dur</th>
              <th>Target Pace</th>
            {/if}
            <th>Distance</th>
            <th>Duration</th>
            <th>Pace</th>
            <th>RPE</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {#each sets as s, i (setId(s))}
            {@const showRec = hasAnyRec(s)}
            <tr>
              <td class="font-mono">{i + 1}</td>

              {#if anyRecVisible}
                {#if !showRec}
                  <td>—</td>
                  <td>—</td>
                  <td>—</td>
                {:else if $isEditing}
                  <td>
                    <input
                      class="input input-sm input-bordered w-20"
                      type="number"
                      value={s.recommendedDistance ?? ''}
                      on:change={(e) => handleUpdate(s, 'recommendedDistance', e.currentTarget.value)}
                    />
                  </td>
                  <td>
                    <input
                      class="input input-sm input-bordered w-24"
                      value={formatTimeSpan(s.recommendedDuration) || ''}
                      placeholder="hh:mm:ss"
                      on:change={(e) => handleUpdate(s, 'recommendedDuration', e.currentTarget.value)}
                    />
                  </td>
                  <td>
                    <input
                      class="input input-sm input-bordered w-24"
                      value={formatTimeSpan(s.targetPace) || ''}
                      placeholder="mm:ss"
                      on:change={(e) => handleUpdate(s, 'targetPace', e.currentTarget.value)}
                    />
                  </td>
                {:else}
                  <td>{s.recommendedDistance ?? '—'}</td>
                  <td>{formatTimeSpan(s.recommendedDuration) || '—'}</td>
                  <td>{formatTimeSpan(s.targetPace) || '—'}</td>
                {/if}
              {/if}

              {#if !$isEditing}
                <td class="relative group">
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
                <td class="relative group">
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
                <td class="relative group">
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
                <td>
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
                <td>
                  <input
                    class="input input-sm input-bordered w-20"
                    type="number"
                    value={s.actualDistance}
                    on:change={(e) => handleUpdate(s, 'actualDistance', e.currentTarget.value)}
                  />
                </td>
                <td>
                  <input
                    class="input input-sm input-bordered w-24"
                    value={formatTimeSpan(s.actualDuration) || ''}
                    placeholder="hh:mm:ss"
                    on:change={(e) => handleUpdate(s, 'actualDuration', e.currentTarget.value)}
                  />
                </td>
                <td>
                  <input
                    class="input input-sm input-bordered w-24"
                    value={formatTimeSpan(s.actualPace) || ''}
                    placeholder="mm:ss"
                    on:change={(e) => handleUpdate(s, 'actualPace', e.currentTarget.value)}
                  />
                </td>
                <td>
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
