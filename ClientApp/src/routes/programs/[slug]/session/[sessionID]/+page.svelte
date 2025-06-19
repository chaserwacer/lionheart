<script lang="ts">
  import { page } from '$app/stores';
  import { onMount } from 'svelte';
  import { goto } from '$app/navigation';
  import { slugify } from '$lib/utils/slugify';
  import CreateMovementModal from '$lib/components/CreateMovement.svelte';
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
  let showUncompleted = true;
  let showCompleted = true;
  let showModal = false;
  const allowedSteps = [1, 5, 10, 25];
  let weightStep: number = 5;

  onMount(async () => {
    slug = $page.params.slug;
    sessionID = $page.params.sessionID;

    const programsClient = new GetTrainingProgramsEndpointClient('http://localhost:5174');
    const sessionClient = new GetTrainingSessionEndpointClient('http://localhost:5174');

    try {
      const allPrograms = await programsClient.getAll3();
      program = allPrograms.find(p => slugify(p.title ?? '') === slug);
      if (!program) return;

      session = await sessionClient.get2(sessionID);
      sessionIndex = program.trainingSessions?.findIndex(s => s.trainingSessionID === sessionID) ?? 0;

      session.movements?.forEach((movement) => {
    movement.sets?.forEach(set => {
      if (set.actualReps === 0) set.actualReps = set.recommendedReps ?? 5;
      if (set.actualRPE === 0) set.actualRPE = set.recommendedRPE ?? 7;
    });
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
      movementModifier: {
        name: movement.movementModifier?.name ?? 'No Modifier',
        equipment: movement.movementModifier?.equipment ?? 'None',
        duration: movement.movementModifier?.duration ?? 0
      },
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

    // Mutate in place
    set.actualReps = set.recommendedReps ?? 5;
    set.actualWeight = set.recommendedWeight ?? 0;
    set.actualRPE = set.recommendedRPE ?? 7;

    // Force reactivity: deep clone of sets to reassign
    session!.movements![mvIndex].sets = [...session!.movements![mvIndex].sets!];

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

    const isRemoved = movement.notes?.startsWith('[REMOVED]');
    movement.notes = isRemoved
      ? (movement.notes ?? '').replace(/^\[REMOVED\] /, '')
      : '[REMOVED] ' + (movement.notes ?? '');


    const updateMovementClient = new UpdateMovementEndpointClient('http://localhost:5174');
    await updateMovementClient.update(UpdateMovementRequest.fromJS(movementToUpdateRequest(movement)));

    session.movements = [...session.movements ?? []]; // force reactivity
}

  function convertWeight(w: number, toUnit: 'kg' | 'lbs'): number {
    return toUnit === 'kg'
      ? +(w * 0.453592).toFixed(1)
      : +(w / 0.453592).toFixed(1);
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

    <div class="flex items-center justify-between mt-8 mb-3">
      <h2 class="text-2xl text-white font-semibold">Uncompleted Movements
        <button
          on:click={() => showUncompleted = !showUncompleted}
          class="text-white text-lg"
        >
          {showUncompleted ? '🡫' : '🡪'}
        </button>
      </h2>
    </div>
  {#if showUncompleted}
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 items-start">
      {#each session.movements ?? [] as movement, mvIndex (movement.movementID)}
        {#if !movement.isCompleted && !movement.notes?.startsWith('[REMOVED]')}
          <div class="bg-zinc-800 text-white rounded-xl p-4 shadow-md max-w-sm mx-auto self-start space-y-3">
            <div class="flex justify-between items-start border-b border-zinc-700 pb-2">
              <div>
                <h2 class="text-xl font-bold">{movement.movementBase?.name ?? 'Unnamed'}</h2>
                <p class="text-sm italic text-gray-400">{movement.movementModifier?.name ?? 'No Modifier'}</p>
              </div>
              <span class="text-sm text-yellow-300">💡 Focus</span>
            </div>
            <div class="flex justify-between items-center text-sm">
              <label class="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={unitMap[mvIndex] === 'kg'}
                  on:change={() => {
                    const newUnit = unitMap[mvIndex] === 'lbs' ? 'kg' : 'lbs';
                    movement.sets?.forEach(set => {
                      set.actualWeight = convertWeight(set.actualWeight ?? 0, newUnit);
                      set.recommendedWeight = convertWeight(set.recommendedWeight ?? 0, newUnit);
                    });
                    unitMap[mvIndex] = newUnit;
                  }}
                />
                Use kg
              </label>

              <div class="flex gap-2">
                <button on:click={() => toggleComplete(mvIndex)} class="text-xs px-2 py-1 rounded bg-green-600 hover:bg-green-500 font-semibold">
                  {movement.isCompleted ? 'Undo' : 'Mark Complete'}
                </button>
                <button on:click={() => toggleRemove(mvIndex)} class="text-xs px-2 py-1 rounded bg-red-600 hover:bg-red-500 font-semibold">
                  Remove
                </button>
              </div>
            </div>

            <ul class="space-y-2">
              {#each movement.sets ?? [] as set, setIndex}
                <li class="bg-zinc-700 p-3 rounded">
                  <div class="grid grid-cols-3 gap-4 text-sm">
                    <div class="flex flex-col gap-1">
                      <span class="text-gray-300">Reps:</span>
                      <input
                        type="number"
                        class="bg-zinc-900 text-white p-1 rounded text-center"
                        bind:value={set.actualReps}
                        on:input={() => updateSetValue(mvIndex, setIndex, 'actualReps', set.actualReps ?? 0)}
                      />
                    </div>
                    <div class="flex flex-col gap-1">
                      <span class="text-gray-300">RPE:</span>
                      <input
                        type="number"
                        class={` text-white p-1 rounded text-center ${getRpeColor(set.actualRPE, set.recommendedRPE)}`}
                        bind:value={set.actualRPE}
                        on:input={() => updateSetValue(mvIndex, setIndex, 'actualRPE', set.actualRPE ?? 0)}
                      />
                    </div>
                    <div class="flex flex-col gap-1">
                      <span class="text-gray-300">Weight:</span>
                      <input
                        type="number"
                        class="bg-zinc-900 text-white p-1 rounded text-center"
                        step={weightStep}
                        bind:value={set.actualWeight}
                        on:input={() => updateSetValue(mvIndex, setIndex, 'actualWeight', set.actualWeight ?? 0)}
                      />
                    </div>
                  </div>
                  <button
                    on:click={() => resetSet(mvIndex, setIndex)}
                    class="text-xs text-red-400 hover:underline mt-1 block text-right"
                  >
                    Reset
                  </button>
                </li>
              {/each}
            </ul>

            <div>
              <label class="text-xs text-gray-300">Notes:</label>
              <textarea
                class="w-full mt-1 bg-zinc-900 text-white p-2 rounded text-sm resize-none"
                rows="2"
                bind:value={movement.notes}
              ></textarea>
            </div>
          </div>

        {/if}
      {/each}
    </div>
    {/if}

    {#if session.movements?.some(m => m.isCompleted)}
    <div class="flex items-center justify-between mt-10 mb-3">
      <h2 class="text-2xl text-white font-semibold">Completed Movements
        <button
        on:click={() => showCompleted = !showCompleted}
        class="text-white text-lg"
      >
        {showCompleted ? '🡫' : '🡪'}
      </button></h2>
    </div>
    {#if showCompleted}
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
    {/if}

  </div>
{:else}
  <div class="p-6 max-w-4xl mx-auto text-red-400">
    <h1 class="text-2xl font-bold">Session not found</h1>
  </div>
{/if}
<button
  on:click={() => showModal = true}
  class="fixed bottom-6 right-6 bg-zinc-100 hover:bg-zinc-400 text-black rounded-full w-12 h-12 text-2xl shadow-lg z-40"
>
  +
</button>

  {#if showModal && session?.trainingSessionID}
  <CreateMovementModal
    show={showModal}
    sessionID={session.trainingSessionID}
    on:close={() => showModal = false}
    on:created={() => location.reload()}
  />
{/if}







