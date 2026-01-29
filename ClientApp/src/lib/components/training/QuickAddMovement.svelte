<script lang="ts">
  import {
    movementBases,
    equipments,
    isLoading,
    newMovementBaseId,
    newEquipmentId,
    newMovementModifier,
    addMovementQuick,
  } from '$lib/stores/sessionStore';
  import { mbId, mbName, equipmentId, equipmentLabel } from '$lib/utils/training';

  export let sessionId: string;

  function handleAddMovement() {
    addMovementQuick(sessionId);
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Enter') {
      e.preventDefault();
      handleAddMovement();
    }
  }
</script>

<div class="card bg-base-100/40 backdrop-blur border border-dashed border-base-content/10 rounded-xl">
  <div class="card-body p-4">
    <div class="flex flex-wrap items-end gap-3">
      <div class="flex-1 min-w-[160px]">
        <span class="block text-xs font-medium text-base-content/50 mb-1">Movement</span>
        <select
          class="select select-sm select-bordered rounded-lg w-full"
          bind:value={$newMovementBaseId}
          on:keydown={handleKeydown}
        >
          {#each $movementBases as mb}
            <option value={mbId(mb)}>{mbName(mb)}</option>
          {/each}
        </select>
      </div>

      <div class="flex-1 min-w-[120px]">
        <span class="block text-xs font-medium text-base-content/50 mb-1">Equipment</span>
        <select
          class="select select-sm select-bordered rounded-lg w-full"
          bind:value={$newEquipmentId}
          on:keydown={handleKeydown}
        >
          {#each $equipments as eq}
            <option value={equipmentId(eq)}>{equipmentLabel(eq)}</option>
          {/each}
        </select>
      </div>

      <div class="flex-1 min-w-[100px]">
        <span class="block text-xs font-medium text-base-content/50 mb-1">Modifier</span>
        <input
          type="text"
          class="input input-sm input-bordered rounded-lg w-full"
          placeholder="e.g. pause, tempo"
          bind:value={$newMovementModifier}
          on:keydown={handleKeydown}
        />
      </div>

      <button
        class="btn btn-sm btn-primary rounded-lg gap-1"
        on:click={handleAddMovement}
        disabled={$isLoading || !$newMovementBaseId || !$newEquipmentId}
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        Add
      </button>
    </div>
  </div>
</div>
