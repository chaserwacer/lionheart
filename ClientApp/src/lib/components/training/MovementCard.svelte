<script lang="ts">
  import type { MovementDTO } from '$lib/api/ApiClient';
  import {
    isEditing,
    dragFromId,
    dragOverId,
    deleteMovement,
    swapMovementsById,
    updateMovement,
    toggleMovementComplete,
    movementBases,
    movementModifiers,
    equipments,
    isLoading,
  } from '$lib/stores/sessionStore';
  import {
    idOfMovement,
    movementBaseName,
    movementEquipmentName,
    movementModifierName,
    movementNotes,
    movementCompleted,
    movementSetCount,
    movementData,
    mbId,
    mbName,
    equipmentId,
    equipmentLabel,
    liftSets,
  } from '$lib/utils/training';
  import LiftSetsTable from './LiftSetsTable.svelte';
  import DTSetsList from './DTSetsList.svelte';

  export let movement: MovementDTO;

  $: id = idOfMovement(movement);
  $: mData = movementData(movement);
  $: isCompleted = movementCompleted(movement);

  // Inline editing state
  let editingField: 'base' | 'equipment' | 'modifier' | 'notes' | null = null;
  let draftNotes = '';

  function startEditNotes() {
    draftNotes = movementNotes(movement);
    editingField = 'notes';
  }

  function cancelEdit() {
    editingField = null;
    draftNotes = '';
  }

  async function saveBase(e: Event) {
    const select = e.target as HTMLSelectElement;
    await updateMovement(id, { movementBaseID: select.value });
    editingField = null;
  }

  async function saveEquipment(e: Event) {
    const select = e.target as HTMLSelectElement;
    await updateMovement(id, { equipmentID: select.value });
    editingField = null;
  }

  async function saveModifier(e: Event) {
    const select = e.target as HTMLSelectElement;
    const val = select.value;
    await updateMovement(id, { movementModifierName: val || null });
    editingField = null;
  }

  async function saveNotes() {
    await updateMovement(id, { notes: draftNotes });
    editingField = null;
    draftNotes = '';
  }

  function handleNotesKeydown(e: KeyboardEvent) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      saveNotes();
    } else if (e.key === 'Escape') {
      cancelEdit();
    }
  }

  // Drag-and-drop (edit mode only)
  function onDragStart(e: DragEvent) {
    if (!$isEditing) return;
    dragFromId.set(id);
    dragOverId.set(null);

    const el = e.currentTarget as HTMLElement | null;
    if (el && e.dataTransfer) {
      const rect = el.getBoundingClientRect();
      e.dataTransfer.setDragImage(el, Math.round(rect.width / 2), 10);
      e.dataTransfer.effectAllowed = 'move';
    }
  }

  function onDragEnter(e: DragEvent) {
    if (!$isEditing) return;
    e.preventDefault();
    if (!$dragFromId || id === $dragFromId) return;
    dragOverId.set(id);
  }

  function onDragOver(e: DragEvent) {
    if (!$isEditing) return;
    e.preventDefault();
    if (!$dragFromId || id === $dragFromId) return;
    dragOverId.set(id);
  }

  function onDragLeave(_e: DragEvent) {
    if ($dragOverId === id) dragOverId.set(null);
  }

  function onDrop(e: DragEvent) {
    if (!$isEditing) return;
    e.preventDefault();
    const fromId = $dragFromId;
    if (!fromId || fromId === id) return;

    swapMovementsById(fromId, id);
    dragFromId.set(null);
    dragOverId.set(null);
  }

  function onDragEnd() {
    dragFromId.set(null);
    dragOverId.set(null);
  }

  function handleDelete() {
    if (!confirm('Remove this movement? This cannot be undone.')) return;
    deleteMovement(id);
  }

  async function handleToggleComplete() {
    await toggleMovementComplete(id);
  }

  // Get current IDs for default select values
  $: currentBaseId = mData?.movementBase?.movementBaseID ?? '';
  $: currentEquipmentId = mData?.equipment?.equipmentID ?? '';
  $: currentModifierName = mData?.movementModifier?.name ?? '';
</script>

<!-- Completed movement view -->
{#if isCompleted && !$isEditing}
  <div
    class="card bg-base-100/40 backdrop-blur border border-base-content/5 rounded-xl overflow-hidden"
    role="listitem"
  >
    <div class="card-body p-4">
      <div class="flex items-start justify-between gap-4">
        <div class="min-w-0 flex-1">
          <div class="flex items-center gap-2">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-success shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
            </svg>
            <span class="text-xl font-display font-bold text-base-content/60 line-through">
              {movementBaseName(movement)}
            </span>
          </div>
          <div class="mt-2 flex flex-wrap gap-2 items-center pl-7">
            <span class="badge badge-sm badge-ghost font-medium">{movementEquipmentName(movement)}</span>
            {#if movementModifierName(movement) && movementModifierName(movement) !== '—'}
              <span class="badge badge-sm badge-ghost font-medium">{movementModifierName(movement)}</span>
            {/if}
          </div>

          <!-- Completed sets summary -->
          {#if liftSets(movement).length > 0}
            <div class="mt-3 pl-7 overflow-x-auto">
              <table class="table table-xs w-auto">
                <thead>
                  <tr class="text-xs font-medium text-base-content/40">
                    <th class="pl-0">Set</th>
                    <th>Reps</th>
                    <th>Weight</th>
                    <th>RPE</th>
                  </tr>
                </thead>
                <tbody>
                  {#each liftSets(movement) as set, index}
                    <tr class="text-base-content/60">
                      <td class="pl-0 font-mono">{index + 1}</td>
                      <td>{set.actualReps ?? '—'}</td>
                      <td>{set.actualWeight ?? '—'}</td>
                      <td>{set.actualRPE ?? '—'}</td>
                    </tr>
                  {/each}
                </tbody>
              </table>
            </div>
          {/if}

          {#if movementNotes(movement)}
            <div class="mt-3 pl-7 text-sm text-base-content/50 italic">
              {movementNotes(movement)}
            </div>
          {/if}
        </div>

        <button
          class="btn btn-sm btn-ghost gap-1 shrink-0"
          on:click={handleToggleComplete}
          disabled={$isLoading}
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
          </svg>
          Edit
        </button>
      </div>
    </div>
  </div>
{:else}
  <!-- Non-completed / editing movement view -->
  <div
    class={'card bg-base-100 border border-base-content/10 rounded-xl overflow-hidden ' +
      ($isEditing ? 'wiggle ring-2 ring-primary/20 ' : '') +
      ($dragOverId === id ? 'swap-hover ' : '') +
      ($dragFromId === id ? 'swap-dragging ' : '')}
    draggable={$isEditing}
    on:dragstart={onDragStart}
    on:dragenter={onDragEnter}
    on:dragover={onDragOver}
    on:dragleave={onDragLeave}
    on:drop={onDrop}
    on:dragend={onDragEnd}
    role="listitem"
  >
    <div class="card-body p-4">
      <div class="flex items-start justify-between gap-4">
        <div class="min-w-0 flex-1">
          <!-- Movement Base Name - clickable to edit -->
          {#if editingField === 'base'}
            <select
              class="select select-bordered select-lg font-display font-bold w-full max-w-md rounded-lg"
              value={currentBaseId}
              on:change={saveBase}
              on:blur={cancelEdit}
              disabled={$isLoading}
            >
              {#each $movementBases as mb}
                <option value={mbId(mb)}>{mbName(mb)}</option>
              {/each}
            </select>
          {:else}
            <button
              type="button"
              class="text-xl sm:text-2xl font-display font-bold leading-tight text-left hover:text-primary transition-colors"
              on:click={() => (editingField = 'base')}
              title="Click to change movement"
            >
              {movementBaseName(movement)}
            </button>
          {/if}

          <!-- Badges row -->
          <div class="mt-2 flex flex-wrap gap-2 items-center">
            <!-- Equipment - clickable -->
            {#if editingField === 'equipment'}
              <select
                class="select select-xs select-bordered rounded-lg"
                value={currentEquipmentId}
                on:change={saveEquipment}
                on:blur={cancelEdit}
                disabled={$isLoading}
            >
              {#each $equipments as eq}
                <option value={equipmentId(eq)}>{equipmentLabel(eq)}</option>
              {/each}
            </select>
          {:else}
            <button
              type="button"
            class="badge badge-sm badge-outline hover:badge-primary transition-colors cursor-pointer"
            on:click={() => (editingField = 'equipment')}
            title="Click to change equipment"
          >
            {movementEquipmentName(movement)}
          </button>
        {/if}

        <!-- Modifier - clickable -->
        {#if editingField === 'modifier'}
          <select
            class="select select-xs select-bordered rounded-lg"
            value={currentModifierName}
            on:change={saveModifier}
            on:blur={cancelEdit}
            disabled={$isLoading}
          >
            <option value="">No modifier</option>
            {#each $movementModifiers as mod}
              <option value={mod.name}>{mod.name}</option>
            {/each}
          </select>
        {:else}
          <button
            type="button"
            class="badge badge-sm badge-outline hover:badge-primary transition-colors cursor-pointer"
            on:click={() => (editingField = 'modifier')}
            title="Click to change modifier"
          >
            {movementModifierName(movement) || 'No modifier'}
          </button>
        {/if}

        <span
          class={'badge badge-sm ' +
            (movementCompleted(movement) ? 'badge-success/20 text-success' : 'badge-ghost text-base-content/50')}
        >
          {movementCompleted(movement) ? '✓ Done' : 'Pending'}
        </span>
        <span class="badge badge-sm badge-ghost text-base-content/50">
          {movementSetCount(movement)} sets
        </span>
      </div>

      <!-- Notes - clickable to edit -->
      {#if editingField === 'notes'}
        <div class="mt-3">
          <textarea
            class="textarea textarea-bordered w-full text-base rounded-lg"
            rows="2"
            bind:value={draftNotes}
            on:keydown={handleNotesKeydown}
            on:blur={saveNotes}
            placeholder="Add notes..."
            disabled={$isLoading}
          />
          <div class="text-xs text-base-content/40 mt-1">
            Press Enter to save, Escape to cancel
          </div>
        </div>
      {:else if movementNotes(movement)}
        <button
          type="button"
          class="mt-3 text-sm text-base-content/60 whitespace-pre-wrap leading-relaxed text-left hover:text-primary transition-colors block w-full"
          on:click={startEditNotes}
          title="Click to edit notes"
        >
          {movementNotes(movement)}
        </button>
      {:else}
        <button
          type="button"
          class="mt-2 text-sm text-base-content/30 hover:text-base-content/50 transition-colors"
          on:click={startEditNotes}
          title="Click to add notes"
        >
          + Add notes
        </button>
      {/if}
    </div>

      <div class="flex items-center gap-2 shrink-0">
        <button
          type="button"
          class="btn btn-sm btn-circle btn-success"
          on:click={handleToggleComplete}
          disabled={$isLoading}
          title="Mark as completed"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
          </svg>
        </button>
        {#if $isEditing}
          <button
            type="button"
            class="btn btn-sm btn-circle btn-ghost text-error hover:bg-error/10"
            on:click={handleDelete}
            title="Delete movement"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
            </svg>
          </button>
        {/if}
      </div>
    </div>

    <!-- SETS -->
    <div class="mt-4 border-t border-base-content/5 pt-4">
      <LiftSetsTable {movement} />
      <DTSetsList {movement} />
    </div>
    </div>
  </div>
{/if}

<style>
  .wiggle {
    animation: none;
  }
  :global(.editing) .wiggle {
    animation: wiggle 0.18s infinite alternate ease-in-out;
    transform-origin: 50% 50%;
  }
  @keyframes wiggle {
    from {
      transform: rotate(-0.6deg);
    }
    to {
      transform: rotate(0.6deg);
    }
  }

  .swap-hover {
    outline: 2px solid hsl(var(--p) / 0.4);
    box-shadow: 0 0 0 4px hsl(var(--p) / 0.1);
  }

  .swap-dragging {
    opacity: 0.85;
    filter: saturate(1.1);
  }
</style>
