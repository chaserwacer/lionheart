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
  let repSchemes: { sets: number; reps: number; rpe: number; weight: number }[] = [];
  const baseUrl = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5174';



  onMount(async () => {
    try {
      const client = new GetMovementBasesEndpointClient(baseUrl);
      movementOptions = await client.getAll();
    } catch (err) {
      console.error('Failed to fetch movement bases', err);
    }
  });

  function addRepScheme() {
  repSchemes = [...repSchemes, { sets: 0, reps: 0, rpe: 0, weight: 0 }];
}


  function removeRepScheme(i: number) {
  repSchemes = repSchemes.filter((_, index) => index !== i);
}


  async function createMovement() {
    const movementBase = movementOptions.find(m => m.movementBaseID === selectedMovementBaseID);
    if (!movementBase ) return;

    const movementClient = new CreateMovementEndpointClient(baseUrl);
    const setClient = new CreateSetEntryEndpointClient(baseUrl);

    const movement = await movementClient.create2(CreateMovementRequest.fromJS({
      movementBaseID: movementBase.movementBaseID,
      trainingSessionID: sessionID,
      notes: '',
      movementModifier: {
        name: modifierName,
        equipment: modifierEquipment || 'none',
        duration: modifierDuration
      },
      weightUnit: WeightUnit._1,
    }));

    for (const scheme of repSchemes) {
      for (let i = 0; i < scheme.sets; i++) {
        await setClient.create3(CreateSetEntryRequest.fromJS({
          movementID: movement.movementID!,
          recommendedReps: scheme.reps,
          recommendedWeight: scheme.weight,
          recommendedRPE: scheme.rpe,
          actualReps: 0,
          actualWeight: 0,
          actualRPE: 0,
        }));
      }
    }

    dispatch('created');
    close();
  }

  function close() {
    dispatch('close');
  }

  let modifierName: string = '';
let modifierEquipment: string = '';
let modifierDuration: number = 0;
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

          
        </div>

        <div class="grid grid-cols-3 gap-4 mb-4">
          <div class="flex flex-col">
            <label class="label label-text mb-1" for="modifier-name">Modifier Name</label>
            <input
              id="modifier-name"
              type="text"
              class="input input-bordered w-full"
              bind:value={modifierName}
              placeholder="e.g. Paused"
            />
          </div>
          <div class="flex flex-col">
            <label class="label label-text mb-1" for="modifier-equipment">Equipment</label>
            <select
              id="modifier-equipment"
              class="select select-bordered w-full"
              bind:value={modifierEquipment}
            >
              <option value="" disabled selected>none</option>
              <option>Kettlebell</option>
              <option>Barbell</option>
              <option>Dumbbell</option>
            </select>
          </div>
          <div class="flex flex-col">
            <label class="label label-text mb-1" for="modifier-duration">Duration (sec)</label>
            <input
              id="modifier-duration"
              type="number"
              min="0"
              class="input input-bordered w-full"
              bind:value={modifierDuration}
              placeholder="0"
            />
          </div>
        </div>

        {#if repSchemes.length}
          <div class="space-y-4">
            {#each repSchemes as rs, i}
              <div class="space-y-1">
                <div class="grid grid-cols-4 gap-4 text-sm font-semibold text-gray-400">
                  <div>Sets</div>
                  <div>Reps</div>
                  <div>RPE</div>
                  <div>Weight</div>
                </div>
                <div class="grid grid-cols-4 gap-4 items-center">
                  <input type="number" min="1" bind:value={rs.sets} class="input input-sm input-bordered text-center" />
                  <input type="number" min="1" bind:value={rs.reps} class="input input-sm input-bordered text-center" />
                  <input type="number" step="0.5" min="1" max="10" bind:value={rs.rpe} class="input input-sm input-bordered text-center" />
                  <input type="number" min="0" step="0.5" bind:value={rs.weight} class="input input-sm input-bordered text-center" />
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

