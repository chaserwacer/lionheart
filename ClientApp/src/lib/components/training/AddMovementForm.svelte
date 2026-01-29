<script lang="ts">
  import {
    movements,
    movementBases,
    equipments,
    isLoading,
    newMovementBaseId,
    newEquipmentId,
    newMovementNotes,
    newModifierText,
    addMovement,
  } from '$lib/stores/sessionStore';
  import { mbId, mbName, equipmentId, equipmentLabel } from '$lib/utils/training';

  export let sessionId: string;

  function handleAddMovement() {
    addMovement(sessionId);
  }
</script>

<div class="mb-8 p-5 rounded-2xl bg-base-200 border border-base-content/10">
  <div class="flex items-center justify-between gap-3 mb-4">
    <div class="text-xs font-mono uppercase tracking-widest text-base-content/50">
      Add Movement
    </div>
    <div class="text-xs text-base-content/50">
      Movements: <span class="font-bold">{$movements?.length ?? 0}</span>
    </div>
  </div>

  <div class="grid grid-cols-1 lg:grid-cols-2 gap-3">
    <div>
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-2">
        Movement Base
      </div>
      <select class="select select-bordered w-full" bind:value={$newMovementBaseId}>
        {#each $movementBases as mb}
          <option value={mbId(mb)}>{mbName(mb)}</option>
        {/each}
      </select>
    </div>

    <div>
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-2">
        Equipment
      </div>
      <select class="select select-bordered w-full" bind:value={$newEquipmentId}>
        {#each $equipments as eq}
          <option value={equipmentId(eq)}>{equipmentLabel(eq)}</option>
        {/each}
      </select>
    </div>

    <!-- groundwork only -->
    <div class="lg:col-span-2">
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-2">
        Modifier (groundwork — not saved yet)
      </div>
      <input
        class="input input-bordered w-full"
        placeholder="e.g., Paused / Tempo / Beltless (future MovementModifier)"
        bind:value={$newModifierText}
      />
    </div>

    <div class="lg:col-span-2">
      <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-2">
        Notes
      </div>
      <input
        class="input input-bordered w-full"
        placeholder="e.g., cues, constraints, intent"
        bind:value={$newMovementNotes}
      />
    </div>

    <div class="lg:col-span-2 flex justify-end">
      <button
        class="btn btn-primary rounded-xl px-5"
        on:click={handleAddMovement}
        disabled={$isLoading || !$newMovementBaseId || !$newEquipmentId}
      >
        Add Movement
      </button>
    </div>
  </div>
</div>
