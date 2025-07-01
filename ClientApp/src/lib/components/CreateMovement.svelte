<script lang="ts">
  import { createEventDispatcher, onMount } from 'svelte';
  import {
    CreateMovementEndpointClient,
    CreateSetEntryEndpointClient,
    GetMovementBasesEndpointClient,
    CreateMovementRequest,
    CreateSetEntryRequest,
    WeightUnit,
    MovementBase,
    MovementModifier
  } from '$lib/api/ApiClient';

  export let show: boolean;
  export let sessionID: string;

  const dispatch = createEventDispatcher();

  let movementOptions: MovementBase[] = [];
  let selectedMovementBaseID: string = '';
  let selectedModifierName: string = 'No Modifier';
  let repSchemes = [{ sets: 1, reps: 5, rpe: 8 }];
  const baseUrl = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5174';

  const modifiers: MovementModifier[] = [
    MovementModifier.fromJS({ name: 'No Modifier', equipment: 'None', duration: 0 }),
    MovementModifier.fromJS({ name: 'Paused', equipment: 'Barbell', duration: 1 }),
    MovementModifier.fromJS({ name: 'Tempo 3-1-0', equipment: 'Barbell', duration: 1 }),
    MovementModifier.fromJS({ name: 'Dumbbell Variant', equipment: 'Dumbbell', duration: 0 }),
    MovementModifier.fromJS({ name: 'Chains Added', equipment: 'Barbell', duration: 0 })
  ];

  onMount(async () => {
    try {
      const client = new GetMovementBasesEndpointClient(baseUrl);
      movementOptions = await client.getAll();
    } catch (err) {
      console.error('Failed to fetch movement bases', err);
    }
  });

  function addRepScheme() {
  repSchemes = [...repSchemes, { sets: 1, reps: 5, rpe: 8 }];
}


  function removeRepScheme(i: number) {
  repSchemes = repSchemes.filter((_, index) => index !== i);
}


  async function createMovement() {
    const movementBase = movementOptions.find(m => m.movementBaseID === selectedMovementBaseID);
    const modifier = modifiers.find(m => m.name === selectedModifierName);
    if (!movementBase || !modifier) return;

    const movementClient = new CreateMovementEndpointClient(baseUrl);
    const setClient = new CreateSetEntryEndpointClient(baseUrl);

    const movement = await movementClient.create2(CreateMovementRequest.fromJS({
      movementBaseID: movementBase.movementBaseID,
      trainingSessionID: sessionID,
      notes: '',
      movementModifier: modifier,
      weightUnit: WeightUnit._1, // Default to LBS
    }));

    for (const scheme of repSchemes) {
      for (let i = 0; i < scheme.sets; i++) {
        await setClient.create3(CreateSetEntryRequest.fromJS({
          movementID: movement.movementID!,
          recommendedReps: scheme.reps,
          recommendedWeight: 0,
          recommendedRPE: scheme.rpe,
          actualReps: scheme.reps,
          actualWeight: 0,
          actualRPE: scheme.rpe,
        }));
      }
    }

    dispatch('created');
    close();
  }

  function close() {
    dispatch('close');
  }
</script>

{#if show}
  <div class="fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center">
    <div class="bg-base-200 text-base-content rounded-lg w-full max-w-xl border border-base-300 max-h-[90vh] flex flex-col">

      <!-- Scrollable body -->
      <div class="p-6 overflow-y-auto" style="max-height: calc(90vh - 4rem);">
        <div class="flex justify-between items-center mb-4">
          <h2 class="text-2xl font-bold">Add Movement to Session</h2>
          <button on:click={close} class="text-gray-400 hover:text-white text-2xl font-bold">&times;</button>
        </div>

        <div class="grid grid-cols-2 gap-4 mb-4">
          <select bind:value={selectedMovementBaseID} class="select select-bordered w-full">
            <option value="">Select a movement</option>
            {#each movementOptions as m}
              <option value={m.movementBaseID}>{m.name}</option>
            {/each}
          </select>

          <select bind:value={selectedModifierName} class="select select-bordered w-full">
            {#each modifiers as mod}
              <option value={mod.name}>{mod.name}</option>
            {/each}
          </select>
        </div>

        {#if repSchemes.length}
          <div class="space-y-4">
            {#each repSchemes as rs, i}
              <div class="space-y-1">
                <div class="grid grid-cols-3 gap-4 text-sm font-semibold text-gray-400">
                  <div class="">Sets</div>
                  <div class="">Reps</div>
                  <div class="">RPE</div>
                </div>

                <div class="grid grid-cols-3 gap-4 items-center">
                  <input type="number" min="1" bind:value={rs.sets} class="input input-sm input-bordered text-center" />
                  <input type="number" min="1" bind:value={rs.reps} class="input input-sm input-bordered text-center" />
                  <input type="number" step="0.5" min="1" max="10" bind:value={rs.rpe} class="input input-sm input-bordered text-center" />
                </div>

                <div class="text-right">
                  <button on:click={() => removeRepScheme(i)} class="text-xs text-red-400 hover:underline">Remove Scheme</button>
                </div>
              </div>
            {/each}
          </div>
        {/if}

        <button on:click={addRepScheme} class="text-sm text-green-500 hover:underline mt-4">+ Add Rep Scheme</button>
      </div>

      <!-- Sticky footer -->
      <div class="p-4 border-t border-base-300 bg-base-100 flex justify-end space-x-2">
        <button on:click={close} class="btn btn-ghost">Cancel</button>
        <button on:click={createMovement} class="btn btn-primary">Add Movement</button>
      </div>
    </div>
  </div>
{/if}

