<script lang="ts">
  import { resolveReference } from '$lib/stores/ingestionStore';
  import type { UnresolvedReference, Resolution } from '$lib/stores/ingestionStore';
  import { resolutions } from '$lib/stores/ingestionStore';

  export let reference: UnresolvedReference;
  export let existingEntities: { id: string; name: string }[] = [];

  $: key = `${reference.entityType}:${reference.rawName}`;
  $: currentResolution = $resolutions[key];
  $: isResolved = currentResolution && (currentResolution.existingId !== null || currentResolution.createNew);

  function handleSelect(e: Event) {
    const value = (e.target as HTMLSelectElement).value;
    if (value === '__create__') {
      resolveReference(reference.entityType, reference.rawName, { existingId: null, createNew: true });
    } else if (value === '') {
      // unresolved
    } else {
      resolveReference(reference.entityType, reference.rawName, { existingId: value, createNew: false });
    }
  }

  function selectedValue(): string {
    if (!currentResolution) return '';
    if (currentResolution.createNew) return '__create__';
    return currentResolution.existingId || '';
  }
</script>

<div class="flex items-center gap-2">
  <span class="badge badge-sm {isResolved ? 'badge-success' : 'badge-warning'}">
    {reference.entityType}
  </span>
  <select
    class="select select-bordered select-sm rounded-lg w-full max-w-xs"
    value={selectedValue()}
    on:change={handleSelect}
  >
    <option value="" disabled>Map "{reference.rawName}" to...</option>

    {#if reference.candidates.length > 0}
      <optgroup label="Suggested matches">
        {#each reference.candidates as candidate}
          <option value={candidate.entityId}>
            {candidate.name} ({Math.round(candidate.confidence * 100)}% match)
          </option>
        {/each}
      </optgroup>
    {/if}

    {#if existingEntities.length > 0}
      <optgroup label="All existing">
        {#each existingEntities as entity}
          <option value={entity.id}>{entity.name}</option>
        {/each}
      </optgroup>
    {/if}

    <optgroup label="New">
      <option value="__create__">Create new: "{reference.rawName}"</option>
    </optgroup>
  </select>
</div>
