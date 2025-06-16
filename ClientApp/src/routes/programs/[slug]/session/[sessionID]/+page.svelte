<script lang="ts">
  import { page } from '$app/stores';
  import { onMount } from 'svelte';
  import { goto } from '$app/navigation';
  import { slugify } from '$lib/utils/slugify';
  import {
    GetTrainingProgramsEndpointClient,
    GetTrainingSessionEndpointClient,
    UpdateTrainingSessionEndpointClient,
    UpdateMovementEndpointClient,
    UpdateSetEntryEndpointClient,
    UpdateSetEntryRequest,
    UpdateMovementRequest,
    UpdateTrainingSessionRequest,
    SetEntry,
    Movement,
    TrainingSession,
    TrainingProgram,
    TrainingSessionStatus
  } from '$lib/api/ApiClient';

  let slug = '';
  let sessionID = '';
  let session: TrainingSession | undefined;
  let sessionIndex = 0;
  let program: TrainingProgram | undefined;

  let unitMap: Record<number, 'lbs' | 'kg'> = {};
  let weightStep: number = 5;
  const allowedSteps = [1, 5, 10, 25];

  onMount(async () => {
    slug = $page.params.slug;
    sessionID = $page.params.sessionID;

    const programsClient = new GetTrainingProgramsEndpointClient('http://localhost:5174');
    const sessionClient = new GetTrainingSessionEndpointClient('http://localhost:5174');

    try {
      const allPrograms = await programsClient.getAll2();
      program = allPrograms.find(p => slugify(p.title ?? '') === slug);
      if (!program) return;

      session = await sessionClient.get2(sessionID);
      sessionIndex = program.trainingSessions?.findIndex(s => s.trainingSessionID === sessionID) ?? 0;

      session.movements?.forEach((_, i) => {
        unitMap[i] = 'lbs';
      });
    } catch (err) {
      console.error('Failed to load session data:', err);
    }
  });

  function movementToUpdateRequest(movement: Movement) {
    return {
      movementID: movement.movementID!,
      trainingSessionID: movement.trainingSessionID!,
      movementBaseID: movement.movementBase?.movementBaseID ?? movement.movementBaseID!,
      movementModifier: movement.movementModifier ?? { name: '', equipment: '', duration: 0 },
      notes: movement.notes ?? '',
      isCompleted: movement.isCompleted ?? false
    };
  }

  function getRpeColor(actual: number | undefined, target: number | undefined): string {
    if (typeof actual !== 'number' || typeof target !== 'number') return 'bg-zinc-900';
    const diff = actual - target;
    if (diff >= 1) return 'bg-red-600';
    if (diff <= -1) return 'bg-blue-600';
    return 'bg-green-600';
  }

  function resetSet(mvIndex: number, setIndex: number) {
    const set = session?.movements?.[mvIndex]?.sets?.[setIndex];
    if (!set?.setEntryID || !set?.movementID) return;

    set.actualReps = set.recommendedReps ?? 5;
    set.actualWeight = set.recommendedWeight ?? 0;
    set.actualRPE = set.recommendedRPE ?? 7;

    const request = UpdateSetEntryRequest.fromJS({
      setEntryID: set.setEntryID,
      movementID: set.movementID,
      recommendedReps: set.recommendedReps ?? 5,
      recommendedWeight: set.recommendedWeight ?? 0,
      recommendedRPE: set.recommendedRPE ?? 7,
      weightUnit: set.weightUnit ?? 1,
      actualReps: set.actualReps,
      actualWeight: set.actualWeight,
      actualRPE: set.actualRPE
    });

    const updateSetClient = new UpdateSetEntryEndpointClient('http://localhost:5174');
    updateSetClient.update2(request).catch((err) => {
      console.error('Failed to reset set', err);
    });
  }

  function updateSetValue(
    mvIndex: number,
    setIndex: number,
    field: keyof Pick<SetEntry, 'actualReps' | 'actualWeight' | 'actualRPE'>,
    value: number
  ) {
    const set = session?.movements?.[mvIndex]?.sets?.[setIndex];
    if (!set?.setEntryID || !set?.movementID) return;

    set[field] = value;

    const request = UpdateSetEntryRequest.fromJS({
      setEntryID: set.setEntryID!,
      movementID: set.movementID!,
      recommendedReps: set.recommendedReps ?? 5,
      recommendedWeight: set.recommendedWeight ?? 100,
      recommendedRPE: set.recommendedRPE ?? 7,
      weightUnit: set.weightUnit ?? 1,
      actualReps: set.actualReps ?? 0,
      actualWeight: set.actualWeight ?? 0,
      actualRPE: set.actualRPE ?? 0
    });

    const updateSetClient = new UpdateSetEntryEndpointClient('http://localhost:5174');
    updateSetClient.update2(request).catch(err => {
      console.error('Failed to update set entry', err);
    });
  }

  async function toggleComplete(index: number) {
    if (!session) return;
    const movement = session.movements?.[index];
    if (!movement?.movementID) return;

    movement.isCompleted = !movement.isCompleted;

    const updateMovementClient = new UpdateMovementEndpointClient('http://localhost:5174');
    await updateMovementClient.update(UpdateMovementRequest.fromJS(movementToUpdateRequest(movement)));

    const allComplete = session.movements?.every(m => m.isCompleted);
    session.status = allComplete ? TrainingSessionStatus._2 : TrainingSessionStatus._0;

    const updateSessionClient = new UpdateTrainingSessionEndpointClient('http://localhost:5174');
    await updateSessionClient.update4(UpdateTrainingSessionRequest.fromJS({
      trainingSessionID: session.trainingSessionID!,
      trainingProgramID: session.trainingProgramID!,
      date: session.date!,
      status: session.status
    }));
  }

  async function toggleRemove(index: number) {
    if (!session) return;
    const movement = session.movements?.[index];
    if (!movement?.movementID) return;

    movement.notes = '[REMOVED] ' + (movement.notes ?? '');

    const updateMovementClient = new UpdateMovementEndpointClient('http://localhost:5174');
    await updateMovementClient.update(UpdateMovementRequest.fromJS(movementToUpdateRequest(movement)));

    session.movements = [...session.movements ?? []];
  }
</script>





{#if session}
  <div class="p-6 max-w-6xl mx-auto">
    <a
      href={`/programs/${slug}`}
      class="inline-flex items-center mb-4 text-sm text-white bg-zinc-700 hover:bg-zinc-600 px-3 py-1 rounded"
    >
      ← Back
    </a>

    <h1 class="text-4xl font-bold mb-6">
      Session {sessionIndex + 1} - {program?.title}
    </h1>

    <div class="mb-4">
      <label class="text-white text-sm mr-2">Weight step increment:</label>
      <select bind:value={weightStep} class="p-1 rounded bg-zinc-900 text-white">
        {#each allowedSteps as step}
          <option value={step}>{step}</option>
        {/each}
      </select>
    </div>

    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 items-start">
      {#each session.movements ?? [] as movement, mvIndex (movement.movementID)}
        {#if !movement.isCompleted && !movement.notes?.startsWith('[REMOVED]')}
          <div class="bg-zinc-800 text-white rounded-xl p-4 shadow-md max-w-sm mx-auto self-start">
            <div class="flex justify-between items-start mb-2">
              <div>
                <h2 class="text-2xl font-bold">{movement.movementBase?.name ?? 'Unnamed'}</h2>
                <p class="text-sm text-gray-400 italic">{movement.movementModifier?.name ?? 'No Modifier'}</p>
              </div>
              <div class="text-sm text-gray-300 italic w-32 text-right">💡 Focus</div>
            </div>

            <div class="flex justify-between items-center mb-2 text-sm">
              <label class="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={unitMap[mvIndex] === 'kg'}
                  on:change={() => unitMap[mvIndex] = unitMap[mvIndex] === 'lbs' ? 'kg' : 'lbs'}
                />
                Use kg
              </label>
              <button on:click={() => toggleComplete(mvIndex)} class="text-xs text-green-400 hover:underline">
                Mark Complete
              </button>
            </div>

            <ul class="space-y-2">
              {#each movement.sets ?? [] as set, setIndex}
                <li class={`bg-zinc-700 p-3 rounded ${setIndex === 0 ? 'border-l-4 border-yellow-400' : ''}`}>
                  <div class="flex flex-wrap gap-3 justify-between items-start text-sm">
                    <div class="flex flex-col gap-1">
                      <span class="text-gray-300">Reps:</span>
                      <input
                        type="number"
                        class="bg-zinc-900 text-white p-1 rounded w-14 text-center"
                        step="1"
                        bind:value={set.actualReps}
                        on:input={() => updateSetValue(mvIndex, setIndex, 'actualReps', set.actualReps ?? 0)}
                      />
                    </div>
                    <div class="flex flex-col gap-1">
                      <span class="text-gray-300">RPE:</span>
                      <input
                        type="number"
                        step="1"
                        class={`text-white p-1 rounded w-14 text-center ${getRpeColor(set.actualRPE, set.recommendedRPE)}`}
                        bind:value={set.actualRPE}
                        on:input={() => updateSetValue(mvIndex, setIndex, 'actualRPE', set.actualRPE ?? 0)}
                      />
                    </div>
                    <div class="flex flex-col gap-1 items-center">
                      <span class="text-gray-300">Weight:</span>
                      <input
                        type="number"
                        class="bg-zinc-900 text-white p-1 rounded w-20 text-center"
                        step={weightStep}
                        bind:value={set.actualWeight}
                        on:input={() => updateSetValue(mvIndex, setIndex, 'actualWeight', set.actualWeight ?? 0)}
                      />
                      <span class="text-xs text-gray-400">
                        Recommended: {set.recommendedWeight} {unitMap[mvIndex]}
                      </span>
                    </div>
                    <button
                      on:click={() => resetSet(mvIndex, setIndex)}
                      class="text-xs text-red-400 hover:underline mt-1"
                    >
                      Reset
                    </button>
                  </div>
                </li>
              {/each}
            </ul>

            <div class="mt-2">
              <label class="text-xs text-gray-300">Notes:</label>
              <textarea
                class="w-full mt-1 bg-zinc-900 text-white p-2 rounded text-sm resize-none"
                rows="2"
                bind:value={movement.notes}
              ></textarea>
            </div>

            <div class="flex justify-end mt-2">
              <button
                on:click={() => toggleRemove(mvIndex)}
                class="text-xs text-red-300 hover:underline"
              >
                Remove Movement
              </button>
            </div>
          </div>
        {/if}
      {/each}
    </div>

    {#if session.movements?.some(m => m.isCompleted)}
      <h2 class="text-2xl text-white font-semibold mt-10 mb-4">✅ Completed Exercises</h2>
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 items-start">
        {#each session.movements as movement, mvIndex (movement.movementID)}
          {#if movement.isCompleted}
            <div class="bg-zinc-700 text-white rounded-xl p-4 shadow max-w-sm mx-auto opacity-60">
              <h3 class="text-xl font-bold">{movement.movementBase?.name ?? 'Unnamed'}</h3>
              <p class="text-sm italic text-gray-300">Marked complete</p>
              <button
                on:click={() => toggleComplete(mvIndex)}
                class="text-xs text-yellow-300 hover:underline mt-2"
              >
                Undo
              </button>
            </div>
          {/if}
        {/each}
      </div>
    {/if}

    {#if session.movements?.some(m => m.notes?.startsWith('[REMOVED]'))}
      <h2 class="text-2xl text-white font-semibold mt-10 mb-4">🗑️ Removed Movements</h2>
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 items-start">
        {#each session.movements as movement, mvIndex (movement.movementID)}
          {#if movement.notes?.startsWith('[REMOVED]')}
            <div class="bg-zinc-700 text-white rounded-xl p-4 shadow max-w-sm mx-auto opacity-50">
              <h3 class="text-xl font-bold">{movement.movementBase?.name ?? 'Unnamed'}</h3>
              <p class="text-sm italic text-gray-300">Marked removed</p>
              <button
                on:click={() => toggleRemove(mvIndex)}
                class="text-xs text-yellow-300 hover:underline mt-2"
              >
                Undo Remove
              </button>
            </div>
          {/if}
        {/each}
      </div>
    {/if}
  </div>
{:else}
  <div class="p-6 max-w-4xl mx-auto text-red-400">
    <h1 class="text-2xl font-bold">Session not found</h1>
  </div>
{/if}

