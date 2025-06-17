<script lang="ts">
  import { createEventDispatcher, onMount } from 'svelte';
  import {
    MovementBase,
    MovementModifier,
    TrainingSessionStatus,
    WeightUnit,
    CreateMovementBaseEndpointClient,
    CreateMovementEndpointClient,
    CreateSetEntryEndpointClient,
    CreateTrainingSessionEndpointClient,
    CreateTrainingSessionRequest,
    CreateMovementBaseRequest,
    CreateMovementRequest,
    CreateSetEntryRequest,
    GetTrainingSessionEndpointClient
  } from '$lib/api/ApiClient';

  export let show: boolean;
  export let programID: string;

  const dispatch = createEventDispatcher();

  let movementOptions: MovementBase[] = [];
  let selectedMovementBaseID: string = '';
  let movementSearch = '';
  export let existingSessionCount: number = 0;

  const modifiers: MovementModifier[] = [
    MovementModifier.fromJS({ name: 'Paused', equipment: 'Barbell', duration: 1 }),
    MovementModifier.fromJS({ name: 'Tempo 3-1-0', equipment: 'Barbell', duration: 1 }),
    MovementModifier.fromJS({ name: 'Dumbbell Variant', equipment: 'Dumbbell', duration: 0 }),
    MovementModifier.fromJS({ name: 'Chains Added', equipment: 'Barbell', duration: 0 })
  ];

  let tempModifierID = '';
  let repSchemeDraft = { sets: 1, reps: 5, rpe: 8 };
  let movements: {
    movementBaseID: string;
    name: string;
    modifierID?: string;
    modifierName?: string;
    repSchemes: { sets: number; reps: number; rpe: number }[];
  }[] = [];

  onMount(async () => {
    try {
      const res = await fetch('http://localhost:5174/api/movement-base/get-all', {
        credentials: 'include'
      });
      movementOptions = await res.json();
    } catch (err) {
      console.error('Failed to fetch movement bases:', err);
    }
  });

  function addMovementByID() {
    const base = movementOptions.find(b => b.movementBaseID === selectedMovementBaseID);
    if (!base || !base.movementBaseID) return; // ensures no undefined gets in
    const matchedMod = modifiers.find(m => m.name === tempModifierID);
    movements = [
      ...movements,
      {
        movementBaseID: base.movementBaseID,
        name: base.name!, 
        modifierID: matchedMod?.name,
        modifierName: matchedMod?.name,
        repSchemes: [{ ...repSchemeDraft }]
      }
    ];
    selectedMovementBaseID = '';
    tempModifierID = '';
  }

  function addRepScheme(index: number) {
    movements[index].repSchemes.push({ sets: 1, reps: 5, rpe: 8 });
    movements = [...movements];
  }

  function removeMovement(index: number) {
    movements = movements.filter((_, i) => i !== index);
  }

  function removeRepScheme(mvIndex: number, rsIndex: number) {
    movements[mvIndex].repSchemes.splice(rsIndex, 1);
    movements = [...movements];
  }

  async function createSession() {
    const sessionClient = new CreateTrainingSessionEndpointClient('http://localhost:5174');
    const movementClient = new CreateMovementEndpointClient('http://localhost:5174');
    const setClient = new CreateSetEntryEndpointClient('http://localhost:5174');

    const sessionDate = new Date();
    sessionDate.setDate(sessionDate.getDate() + 2);

    const sessionRequest = CreateTrainingSessionRequest.fromJS({
      trainingProgramID: programID,
      date: sessionDate
  });

  const session = await sessionClient.create4(sessionRequest);

  for (const movement of movements) {
    const modifier = modifiers.find(m => m.name === movement.modifierID);
    const modifierModel = new MovementModifier({
      name: modifier?.name ?? '',
      equipment: modifier?.equipment ?? '',
      duration: modifier?.duration ?? 0
    });

    const movementReq = CreateMovementRequest.fromJS({
      movementBaseID: movement.movementBaseID,
      trainingSessionID: session.trainingSessionID!,
      notes: '',
      movementModifier: modifierModel
    });

    const movementResult = await movementClient.create(movementReq);

    for (const scheme of movement.repSchemes) {
      for (let i = 0; i < scheme.sets; i++) {
        const setReq = CreateSetEntryRequest.fromJS({
          movementID: movementResult.movementID!,
          recommendedReps: scheme.reps,
          recommendedWeight: 0,
          recommendedRPE: scheme.rpe,
          weightUnit: WeightUnit._1,
          actualReps: 0,
          actualWeight: 0,
          actualRPE: 0
        });
        await setClient.create2(setReq);
      }
    }
  }

  const getSessionClient = new GetTrainingSessionEndpointClient('http://localhost:5174');
  const fullSession = await getSessionClient.get2(session.trainingSessionID!);

  dispatch('createdWithSession', {
    sessionID: session.trainingSessionID!,
    sessionNumber: existingSessionCount + 1,
    date: session.date,
  });

  close();
}
function close() {
  dispatch('close');
}


</script>

{#if show}
  <div class="fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center">
    <div class="bg-zinc-900 rounded-lg p-6 w-full max-w-2xl text-white border border-zinc-700">
      <div class="flex justify-between items-center mb-4">
        <h2 class="text-2xl font-bold">Create New Training Session</h2>
        <button on:click={close} class="text-gray-400 hover:text-white text-2xl font-bold">&times;</button>
      </div>

      <div class="grid grid-cols-2 gap-4 mb-4">
        <select bind:value={selectedMovementBaseID} class="w-full bg-zinc-800 border border-zinc-600 p-2 rounded text-white">
          <option value="">Select a movement</option>
          {#each movementOptions as m}
            <option value={m.movementBaseID}>{m.name}</option>
          {/each}
        </select>

        <select bind:value={tempModifierID} class="w-full bg-zinc-800 border border-zinc-600 p-2 rounded text-white">
          <option value="">No modifier</option>
          {#each modifiers as mod}
            <option value={mod.name}>{mod.name}</option>
          {/each}
        </select>
      </div>

      <button on:click={addMovementByID} class="text-sm text-green-400 hover:underline mb-6">+ Add Movement</button>

      {#if movements.length}
        <div class="space-y-4">
          {#each movements as m, i}
            <div class="border border-zinc-700 p-4 rounded bg-zinc-800">
              <div class="flex justify-between items-center mb-2">
                <strong class="text-lg">{m.name} {#if m.modifierName}– <span class="italic text-sm text-gray-400">{m.modifierName}</span>{/if}</strong>
                <button on:click={() => removeMovement(i)} class="text-red-400 hover:underline text-sm">Remove</button>
              </div>
              <div class="space-y-2">
                {#each m.repSchemes as rs, j}
                  <div class="grid grid-cols-3 gap-4 items-center">
                    <input type="number" min="1" bind:value={rs.sets} class="p-2 bg-zinc-900 text-white border border-zinc-700 rounded text-center" placeholder="Sets" />
                    <input type="number" min="1" bind:value={rs.reps} class="p-2 bg-zinc-900 text-white border border-zinc-700 rounded text-center" placeholder="Reps" />
                    <input type="number" step="0.5" min="1" max="10" bind:value={rs.rpe} class="p-2 bg-zinc-900 text-white border border-zinc-700 rounded text-center" placeholder="RPE" />
                    <button on:click={() => removeRepScheme(i, j)} class="text-xs text-red-300 hover:underline">Remove Scheme</button>
                  </div>
                {/each}
                <button on:click={() => addRepScheme(i)} class="text-sm text-green-400 hover:underline mt-2">+ Add Rep Scheme</button>
              </div>
            </div>
          {/each}
        </div>
      {/if}

      <div class="flex justify-end space-x-2 mt-6">
        <button on:click={close} class="px-4 py-2 bg-zinc-700 text-white rounded hover:bg-zinc-600">Cancel</button>
        <button on:click={createSession} class="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-500">Create Session</button>
      </div>
    </div>
  </div>
{/if}
