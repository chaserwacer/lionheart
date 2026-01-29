<script lang="ts">
  import type { MovementDTO } from '$lib/api/ApiClient';
  import { isEditing, updateDtSet, deleteDtSet } from '$lib/stores/sessionStore';
  import { setId } from '$lib/utils/training';
  import { formatTimeSpan, parseNumberOrZero } from '$lib/utils/training';

  export let movement: MovementDTO;
  export let set: any;
  export let index: number;

  function handleUpdate(field: string, value: string) {
    if (field === 'actualDistance' || field === 'actualRPE' || field === 'intervalType' || field === 'distanceUnit') {
      updateDtSet(movement, set, { [field]: parseNumberOrZero(value) });
    } else {
      updateDtSet(movement, set, { [field]: value });
    }
  }

  function handleDelete() {
    if (!confirm('Delete this set?')) return;
    deleteDtSet(movement, set);
  }
</script>

<div class="p-3 rounded-xl bg-base-200 border border-base-content/10">
  <div class="flex items-start justify-between gap-3">
    <div class="min-w-0">
      <div class="font-mono text-xs uppercase tracking-widest text-base-content/50">
        DT Set {index + 1}
      </div>
    </div>
    <button
      class="btn btn-xs btn-outline btn-error"
      type="button"
      on:click={handleDelete}
    >
      ×
    </button>
  </div>

  <div class="mt-3 grid grid-cols-1 md:grid-cols-3 gap-3">
    <!-- Recommended Distance -->
    <div>
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1">
        Recommended Distance
      </div>
      <div class="text-base font-semibold">{set.recommendedDistance}</div>
    </div>

    <!-- Actual Distance -->
    <div>
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1">
        Actual Distance
      </div>
      {#if !$isEditing}
        <div class="text-base font-semibold">{set.actualDistance}</div>
      {:else}
        <input
          class="input input-sm input-bordered w-full"
          type="number"
          value={set.actualDistance}
          on:change={(e) => handleUpdate('actualDistance', e.currentTarget.value)}
        />
      {/if}
    </div>

    <!-- Actual RPE -->
    <div>
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1">
        Actual RPE
      </div>
      {#if !$isEditing}
        <div class="text-base font-semibold">{set.actualRPE}</div>
      {:else}
        <input
          class="input input-sm input-bordered w-full"
          type="number"
          step="0.5"
          value={set.actualRPE}
          on:change={(e) => handleUpdate('actualRPE', e.currentTarget.value)}
        />
      {/if}
    </div>

    <!-- Interval Duration -->
    <div>
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1">
        Interval Duration
      </div>
      {#if !$isEditing}
        <div class="text-base font-semibold">{formatTimeSpan(set.intervalDuration)}</div>
      {:else}
        <input
          class="input input-sm input-bordered w-full"
          value={formatTimeSpan(set.intervalDuration)}
          placeholder="hh:mm:ss"
          on:change={(e) => handleUpdate('intervalDuration', e.currentTarget.value)}
        />
      {/if}
    </div>

    <!-- Target Pace -->
    <div>
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1">
        Target Pace
      </div>
      {#if !$isEditing}
        <div class="text-base font-semibold">{formatTimeSpan(set.targetPace)}</div>
      {:else}
        <input
          class="input input-sm input-bordered w-full"
          value={formatTimeSpan(set.targetPace)}
          placeholder="hh:mm:ss"
          on:change={(e) => handleUpdate('targetPace', e.currentTarget.value)}
        />
      {/if}
    </div>

    <!-- Actual Pace -->
    <div>
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1">
        Actual Pace
      </div>
      {#if !$isEditing}
        <div class="text-base font-semibold">{formatTimeSpan(set.actualPace)}</div>
      {:else}
        <input
          class="input input-sm input-bordered w-full"
          value={formatTimeSpan(set.actualPace)}
          placeholder="hh:mm:ss"
          on:change={(e) => handleUpdate('actualPace', e.currentTarget.value)}
        />
      {/if}
    </div>

    <!-- Recommended Duration -->
    <div>
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1">
        Recommended Duration
      </div>
      <div class="text-base font-semibold">{formatTimeSpan(set.recommendedDuration)}</div>
    </div>

    <!-- Actual Duration -->
    <div>
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1">
        Actual Duration
      </div>
      {#if !$isEditing}
        <div class="text-base font-semibold">{formatTimeSpan(set.actualDuration)}</div>
      {:else}
        <input
          class="input input-sm input-bordered w-full"
          value={formatTimeSpan(set.actualDuration)}
          placeholder="hh:mm:ss"
          on:change={(e) => handleUpdate('actualDuration', e.currentTarget.value)}
        />
      {/if}
    </div>

    <!-- Recommended Rest -->
    <div>
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1">
        Recommended Rest
      </div>
      <div class="text-base font-semibold">{formatTimeSpan(set.recommendedRest)}</div>
    </div>

    <!-- Actual Rest -->
    <div>
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1">
        Actual Rest
      </div>
      {#if !$isEditing}
        <div class="text-base font-semibold">{formatTimeSpan(set.actualRest)}</div>
      {:else}
        <input
          class="input input-sm input-bordered w-full"
          value={formatTimeSpan(set.actualRest)}
          placeholder="hh:mm:ss"
          on:change={(e) => handleUpdate('actualRest', e.currentTarget.value)}
        />
      {/if}
    </div>

    <!-- Interval Type -->
    <div>
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1">
        Interval Type
      </div>
      {#if !$isEditing}
        <div class="text-base font-semibold">{set.intervalType}</div>
      {:else}
        <input
          class="input input-sm input-bordered w-full"
          value={String(set.intervalType)}
          on:change={(e) => handleUpdate('intervalType', e.currentTarget.value)}
        />
      {/if}
    </div>

    <!-- Distance Unit -->
    <div>
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-1">
        Distance Unit
      </div>
      {#if !$isEditing}
        <div class="text-base font-semibold">{set.distanceUnit}</div>
      {:else}
        <input
          class="input input-sm input-bordered w-full"
          value={String(set.distanceUnit)}
          on:change={(e) => handleUpdate('distanceUnit', e.currentTarget.value)}
        />
      {/if}
    </div>
  </div>
</div>
