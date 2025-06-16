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
    GetMovementBasesEndpointClient,
    CreateTrainingSessionRequest,
    CreateMovementBaseRequest,
  CreateMovementRequest,
  CreateSetEntryRequest
  } from '$lib/api/ApiClient';

  export let show: boolean;
  export let programID: string;

  const dispatch = createEventDispatcher();

  let movementSearch = '';
  let filteredOptions: MovementBase[] = []; 

  const modifiers: MovementModifier[] = [
    MovementModifier.fromJS({ name: 'Paused', equipment: 'Barbell', duration: 1 }),
    MovementModifier.fromJS({ name: 'Tempo 3-1-0', equipment: 'Barbell', duration: 1 }),
    MovementModifier.fromJS({ name: 'Dumbbell Variant', equipment: 'Dumbbell', duration: 0 }),
    MovementModifier.fromJS({ name: 'Chains Added', equipment: 'Barbell', duration: 0 })
  ];

  let tempModifierID = '';
  let tempModifierName = '';

  let repSchemeDraft = { sets: 1, reps: 5, rpe: 8 };

  let movements: {
    name: string;
    modifierID?: string;
    modifierName?: string;
    repSchemes: { sets: number; reps: number; rpe: number }[];
  }[] = [];

  $: filteredOptions = movementSearch.length
    ? movementOptions.filter(m => m.name?.toLowerCase().includes(movementSearch.toLowerCase()))
    : movementOptions;

  const movementBaseClient = new GetMovementBasesEndpointClient('http://localhost:5174');
  let movementOptions: MovementBase[] = [];

onMount(async () => {
  try {
    movementOptions = await movementBaseClient.getMovementBases(); // ✅ Use this
  } catch (err) {
    console.error('Failed to fetch movement bases:', err);
  }
});

  function addMovement(name: string) {
    if (!name) return;
    const matchedMod = modifiers.find(m => m.name === tempModifierID);
    const newMovement = {
      name,
      modifierID: matchedMod ? tempModifierID : undefined,
      modifierName: matchedMod?.name,
      repSchemes: [{ ...repSchemeDraft }]
    };
    movements = [...movements, newMovement];
    movementSearch = '';
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
  const movementBaseClient = new CreateMovementBaseEndpointClient('http://localhost:5174');
  const movementClient = new CreateMovementEndpointClient('http://localhost:5174');
  const setClient = new CreateSetEntryEndpointClient('http://localhost:5174');

  // 1. Create the training session
  const sessionDate = new Date();
  sessionDate.setDate(sessionDate.getDate() + 2);

  const sessionRequest = CreateTrainingSessionRequest.fromJS({
    trainingProgramID: programID,
    date: sessionDate
  });

  const session = await sessionClient.create4(sessionRequest);

  // 2. Loop through the UI-defined movements
  for (const movement of movements) {
    // a. Ensure the movement base exists (create if missing)
    let base = movementOptions.find(b => b.name === movement.name);
    if (!base) {
      const baseReq = CreateMovementBaseRequest.fromJS({ name: movement.name });
      base = await movementBaseClient.createMovementBase(baseReq);
    }

    // b. Build the movement modifier
    const modifier = modifiers.find(m => m.name === movement.modifierID);
    const modifierModel = new MovementModifier({
      name: modifier?.name ?? '',
      equipment: modifier?.equipment ?? '',
      duration: modifier?.duration ?? 0
    });

    // c. Create the movement
    const movementReq = CreateMovementRequest.fromJS({
      movementBaseID: base.movementBaseID!,
      trainingSessionID: session.trainingSessionID!,
      notes: '',
      movementModifier: modifierModel
    });

    const movementResult = await movementClient.create(movementReq);



    // d. Create each set for this movement
    for (const scheme of movement.repSchemes) {
      for (let i = 0; i < scheme.sets; i++) {
        const setReq = CreateSetEntryRequest.fromJS({
          movementID: movementResult.movementID!,
          recommendedReps: scheme.reps,
          recommendedWeight: 0,
          recommendedRPE: scheme.rpe,
          weightUnit: WeightUnit._1, // _1 = Pounds
          actualReps: 0,
          actualWeight: 0,
          actualRPE: 0
        });

        await setClient.create2(setReq);
      }
    }
  }

  dispatch('created');
  dispatch('close');
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

      <input
        type="text"
        placeholder="Search for a movement..."
        bind:value={movementSearch}
        class="w-full mb-2 p-2 rounded bg-zinc-800 border border-zinc-600 text-white"
      />

      {#if filteredOptions.length > 0}
        <ul class="max-h-40 overflow-y-auto bg-zinc-800 border border-zinc-700 rounded mb-2">
          {#each filteredOptions as option}
            <li>
              <button
                type="button"
                class="w-full text-left px-4 py-2 hover:bg-zinc-700 cursor-pointer bg-transparent text-white"
                on:click={() => addMovement(option.name ?? '')}
              >
                {option.name}
              </button>
            </li>
          {/each}
        </ul>
      {/if}


      <select bind:value={tempModifierID} class="w-full bg-zinc-800 border border-zinc-600 p-2 rounded text-white mb-4">
        <option value="">No modifier</option>
        {#each modifiers as mod}
          <option value={mod.name}>{mod.name}</option>
        {/each}
      </select>

      {#if movements.length}
        <div class="space-y-4">
          {#each movements as m, i}
            <div class="border border-zinc-700 p-4 rounded bg-zinc-800">
              <div class="flex justify-between items-center mb-2">
                <strong class="text-lg">
                  {m.name}
                  {#if m.modifierID}
                    – <span class="italic text-sm text-gray-400">{modifiers.find(mod => mod.name === m.modifierID)?.name}</span>
                  {/if}
                </strong>
                <button on:click={() => removeMovement(i)} class="text-red-400 hover:underline text-sm">Remove</button>
              </div>
              <div class="space-y-2">
                {#each m.repSchemes as rs, j}
                  <div class="grid grid-cols-3 gap-4 items-center">
                    <input
                      type="number"
                      min="1"
                      bind:value={rs.sets}
                      class="p-2 bg-zinc-900 text-white border border-zinc-700 rounded text-center"
                      placeholder="Sets"
                    />
                    <input
                      type="number"
                      min="1"
                      bind:value={rs.reps}
                      class="p-2 bg-zinc-900 text-white border border-zinc-700 rounded text-center"
                      placeholder="Reps"
                    />
                    <input
                      type="number"
                      step="0.5"
                      min="1"
                      max="10"
                      bind:value={rs.rpe}
                      class="p-2 bg-zinc-900 text-white border border-zinc-700 rounded text-center"
                      placeholder="RPE"
                    />
                    <button
                      on:click={() => removeRepScheme(i, j)}
                      class="text-xs text-red-300 hover:underline"
                    >
                      Remove Scheme
                    </button>
                  </div>
                {/each}
                <button on:click={() => addRepScheme(i)} class="text-sm text-green-400 hover:underline mt-2">
                  + Add Rep Scheme
                </button>
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



