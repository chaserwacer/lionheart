<script lang="ts">
  import { createEventDispatcher } from 'svelte';
  import { v4 as uuid } from 'uuid';
  import type { Movement, MovementBase, MovementModifier, SetEntry, TrainingSession } from '$lib/types/programs';

  export let show: boolean;
  export let programID: string;

  const dispatch = createEventDispatcher();

  const movementOptions: MovementBase[] = [
    { movementBaseID: 'squat', name: 'Squat' },
    { movementBaseID: 'bench', name: 'Bench Press' },
    { movementBaseID: 'deadlift', name: 'Deadlift' },
    { movementBaseID: 'row', name: 'Barbell Row' },
    { movementBaseID: 'ohp', name: 'Overhead Press' }
  ];

  const modifierOptions: MovementModifier[] = [
    { movementModifierID: 'paused', name: 'Paused', equipment: 'Barbell', duration: 1 },
    { movementModifierID: 'tempo', name: 'Tempo 3-1-0', equipment: 'Barbell', duration: 1 },
    { movementModifierID: 'dumbbell', name: 'Dumbbell Variant', equipment: 'Dumbbell', duration: 0 },
    { movementModifierID: 'chains', name: 'Chains Added', equipment: 'Barbell', duration: 0 }
  ];

  let movementSearch = '';
  let filteredOptions: MovementBase[] = movementOptions;

  $: filteredOptions = movementSearch.length
    ? movementOptions.filter(m => m.name.toLowerCase().includes(movementSearch.toLowerCase()))
    : movementOptions;

  let repSchemeDraft = {
    sets: 1,
    reps: 5,
    rpe: 8
  };

  let tempModifierID = '';

  let movements: {
    name: string;
    modifierID?: string;
    repSchemes: { sets: number; reps: number; rpe: number }[];
  }[] = [];

  function addMovement(name: string) {
    if (!name) return;

    const newMovement = {
      name,
      modifierID: tempModifierID || undefined,
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

  function createSession() {
    const sessionID = uuid();

    const builtMovements: Movement[] = movements.map((m) => {
      const movementID = uuid();
      const base = movementOptions.find(base => base.name === m.name) ?? {
        movementBaseID: uuid(),
        name: m.name
      };

      const modifier = modifierOptions.find(mod => mod.movementModifierID === m.modifierID);

      const sets: SetEntry[] = m.repSchemes.flatMap(scheme =>
        Array.from({ length: scheme.sets }, () => ({
          setEntryID: uuid(),
          movementID,
          recommendedReps: scheme.reps,
          recommendedWeight: 0,
          recommendedRPE: scheme.rpe,
          weightUnit: 'Pounds',
          actualReps: 0,
          actualWeight: 0,
          actualRPE: 0
        }))
      );

      return {
        movementID,
        trainingSessionID: sessionID,
        movementBaseID: base.movementBaseID,
        movementBase: base,
        movementModifierID: modifier?.movementModifierID,
        movementModifier: modifier,
        sets,
        notes: '',
        completed: false,
        removed: false
      };
    });

    const date = new Date();
    date.setDate(date.getDate() + 2);

    const newSession: TrainingSession = {
    sessionID,
    programID,
    date: date.toISOString(),
    movements: builtMovements,
    status: 'Planned'
  };


    dispatch('created', newSession);
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
            <li
              class="px-4 py-2 hover:bg-zinc-700 cursor-pointer"
              on:click={() => addMovement(option.name)}
            >
              {option.name}
            </li>
          {/each}
        </ul>
      {/if}

      <select bind:value={tempModifierID} class="w-full bg-zinc-800 border border-zinc-600 p-2 rounded text-white mb-4">
        <option value="">No modifier</option>
        {#each modifierOptions as mod}
          <option value={mod.movementModifierID}>{mod.name}</option>
        {/each}
      </select>

      {#if movements.length}
        <div class="space-y-4">
          {#each movements as m, i}
            <div class="border border-zinc-700 p-4 rounded bg-zinc-800">
              <div class="flex justify-between items-center mb-2">
                <strong class="text-lg">{m.name} {#if m.modifierID} – <span class="italic text-sm text-gray-400">{modifierOptions.find(mod => mod.movementModifierID === m.modifierID)?.name}</span>{/if}</strong>
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
