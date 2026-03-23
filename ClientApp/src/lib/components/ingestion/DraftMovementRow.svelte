<script lang="ts">
  import type { DraftMovement, UnresolvedReference } from '$lib/stores/ingestionStore';
  import { unresolvedRefs, existingMovementBases, existingEquipment, existingModifiers } from '$lib/stores/ingestionStore';
  import ResolutionDropdown from './ResolutionDropdown.svelte';
  import DraftLiftSetTable from './DraftLiftSetTable.svelte';

  export let movement: DraftMovement;
  export let sessionIdx: number;
  export let movementIdx: number;
  export let onUpdate: (patch: Partial<DraftMovement>) => void;
  export let onRemove: () => void;

  $: baseRef = $unresolvedRefs.find(
    r => r.entityType === 'MovementBase' && r.rawName === movement.movementBaseName
  );
  $: equipRef = $unresolvedRefs.find(
    r => r.entityType === 'Equipment' && r.rawName === movement.equipmentName
  );
  $: modRef = movement.modifierName
    ? $unresolvedRefs.find(
        r => r.entityType === 'MovementModifier' && r.rawName === movement.modifierName
      )
    : null;

  $: baseEntities = $existingMovementBases.map(b => ({ id: b.movementBaseID, name: b.name }));
  $: equipEntities = $existingEquipment.map(e => ({ id: e.equipmentID, name: e.name }));
  $: modEntities = $existingModifiers.map(m => ({ id: m.movementModifierID, name: m.name }));

  $: setCount = (movement.liftSets?.length || 0) + (movement.distanceTimeSets?.length || 0);
  $: hasUnresolved = !!baseRef || !!equipRef || !!modRef;

  function handleLiftSetsUpdate(sets: any[]) {
    onUpdate({ liftSets: sets });
  }
</script>

<div class="border border-base-content/10 rounded-xl p-3 {hasUnresolved ? 'border-warning/40 bg-warning/5' : ''}">
  <div class="flex items-center justify-between gap-2 mb-2">
    <div class="flex items-center gap-2 flex-wrap">
      <span class="font-medium">{movement.movementBaseName}</span>
      {#if movement.equipmentName && movement.equipmentName !== 'Bodyweight'}
        <span class="text-sm text-base-content/50">— {movement.equipmentName}</span>
      {/if}
      {#if movement.modifierName}
        <span class="badge badge-sm badge-ghost">{movement.modifierName}</span>
      {/if}
      <span class="text-xs text-base-content/40">{setCount} set{setCount !== 1 ? 's' : ''}</span>
    </div>
    <button class="btn btn-ghost btn-xs text-error" on:click={onRemove}>&times;</button>
  </div>

  <!-- Resolution dropdowns for unresolved references -->
  {#if baseRef}
    <div class="mb-2">
      <ResolutionDropdown reference={baseRef} existingEntities={baseEntities} />
    </div>
  {/if}
  {#if equipRef}
    <div class="mb-2">
      <ResolutionDropdown reference={equipRef} existingEntities={equipEntities} />
    </div>
  {/if}
  {#if modRef}
    <div class="mb-2">
      <ResolutionDropdown reference={modRef} existingEntities={modEntities} />
    </div>
  {/if}

  <!-- Lift sets table -->
  {#if movement.liftSets && movement.liftSets.length > 0}
    <DraftLiftSetTable
      sets={movement.liftSets}
      onUpdate={handleLiftSetsUpdate}
    />
  {/if}

  <!-- DT sets summary -->
  {#if movement.distanceTimeSets && movement.distanceTimeSets.length > 0}
    <div class="text-sm text-base-content/60 mt-1">
      {movement.distanceTimeSets.length} distance/time set{movement.distanceTimeSets.length !== 1 ? 's' : ''}
    </div>
  {/if}

  <!-- Notes -->
  {#if movement.notes}
    <p class="text-xs text-base-content/40 mt-1 italic">{movement.notes}</p>
  {/if}
</div>
