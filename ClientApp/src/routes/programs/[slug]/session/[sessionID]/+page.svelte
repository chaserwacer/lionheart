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
    import { base } from '$app/paths';

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
  const baseUrl = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5174';

  onMount(async () => {
    slug = $page.params.slug;
    sessionID = $page.params.sessionID;

    const programsClient = new GetTrainingProgramsEndpointClient(baseUrl);
    const sessionClient = new GetTrainingSessionEndpointClient(baseUrl);

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
    if (typeof actual !== 'number' || typeof target !== 'number') return 'bg-zinc-800';

    const diff = actual - target;

    if (diff >= 1) return 'bg-rose-600';     // Overshot RPE → Bright red-pink
    if (diff <= -1) return 'bg-sky-500';      // Undershot RPE → Bright blue
    return 'bg-emerald-600';                 // On target → Neon green
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

    const updateSetClient = new UpdateSetEntryEndpointClient(baseUrl);
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

    const updateSetClient = new UpdateSetEntryEndpointClient(baseUrl);
    updateSetClient.update2(request).catch(err => {
      console.error('Failed to update set entry', err);
    });
  }

  async function toggleComplete(index: number) {
    if (!session) return;
    const movement = session.movements?.[index];
    if (!movement?.movementID) return;

    movement.isCompleted = !movement.isCompleted;

    
    const updateMovementClient = new UpdateMovementEndpointClient(baseUrl);

    await updateMovementClient.update(UpdateMovementRequest.fromJS(movementToUpdateRequest(movement)));

    const allComplete = session.movements?.every(m => m.isCompleted);
    session.status = allComplete ? TrainingSessionStatus._2 : TrainingSessionStatus._0;

    const updateSessionClient = new UpdateTrainingSessionEndpointClient(baseUrl);
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


    const updateMovementClient = new UpdateMovementEndpointClient(baseUrl);
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
      class="inline-flex items-center text-sm text-white bg-zinc-700 hover:bg-zinc-600 px-3 py-1 rounded"
    >
      ← Back to Program
    </a>

    <div class="flex flex-col sm:flex-row sm:justify-between sm:items-center mb-6 gap-3">
      <h1 class="text-3xl sm:text-4xl font-bold text-white">
        Session {sessionIndex + 1} <span class="text-gray-400">· {program?.title}</span>
      </h1>
      <div class="flex items-center gap-2">
        <label class="text-sm text-white">Weight step:</label>
        <select bind:value={weightStep} class="bg-zinc-800 text-white p-2 rounded border border-zinc-600">
          {#each allowedSteps as step}
            <option value={step}>{step}</option>
          {/each}
        </select>
      </div>
    </div>

    <div class="flex justify-between items-center mb-4">
      <h2 class="text-xl text-white font-semibold">Uncompleted Movements
        <button
          on:click={() => showUncompleted = !showUncompleted}
          class="text-white text-lg hover:text-gray-300"
        >
          {showUncompleted ? '▾' : '▸'}
        </button>
      </h2>
    </div>

    {#if showUncompleted}
      <div class="flex flex-wrap gap-6 items-start">

        {#each session.movements ?? [] as movement, mvIndex (movement.movementID)}
          {#if !movement.isCompleted && !movement.notes?.startsWith('[REMOVED]')}
            <div class="bg-zinc-900 border border-zinc-700 rounded-xl p-5 text-white shadow transition hover:shadow-lg w-full sm:w-[350px]">

              <div class="flex justify-between items-start mb-3">
                <div>
                  <h3 class="text-xl font-bold">{movement.movementBase?.name ?? 'Unnamed'}</h3>
                  <p class="text-sm text-gray-400 italic">{movement.movementModifier?.name ?? 'No Modifier'}</p>
                </div>
                <span class="text-sm text-yellow-300">💡 Focus</span>
              </div>

              <div class="flex justify-between items-center mb-4 text-sm">
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
                  <button
                    on:click={() => toggleComplete(mvIndex)}
                    class="text-xs px-3 py-1 bg-emerald-600 hover:bg-emerald-500 rounded font-semibold"
                  >
                    {movement.isCompleted ? 'Undo' : 'Complete'}
                  </button>
                  <button
                    on:click={() => toggleRemove(mvIndex)}
                    class="text-xs px-3 py-1 bg-rose-600 hover:bg-rose-500 rounded font-semibold"
                  >
                    Remove
                  </button>
                </div>
              </div>

              <ul class="space-y-3">
                {#each movement.sets ?? [] as set, setIndex}
                  <li class="bg-zinc-800 border border-zinc-700 p-3 rounded">
                    <div class="grid grid-cols-3 gap-4 text-sm">
                      <div class="flex flex-col gap-1">
                        <label class="text-gray-400">Reps</label>
                        <input
                          type="number"
                          class="bg-zinc-900 border border-zinc-700 rounded p-1 text-white text-center"
                          bind:value={set.actualReps}
                          on:input={() => updateSetValue(mvIndex, setIndex, 'actualReps', set.actualReps ?? 0)}
                        />
                      </div>
                      <div class="flex flex-col gap-1">
                        <label class="text-gray-400">RPE</label>
                        <input
                          type="number"
                          class={`border border-zinc-700 rounded p-1 text-center text-white ${getRpeColor(set.actualRPE, set.recommendedRPE)}`}
                          bind:value={set.actualRPE}
                          on:input={() => updateSetValue(mvIndex, setIndex, 'actualRPE', set.actualRPE ?? 0)}
                        />
                      </div>
                      <div class="flex flex-col gap-1">
                        <label class="text-gray-400">Weight</label>
                        <input
                          type="number"
                          step={weightStep}
                          class="bg-zinc-900 border border-zinc-700 rounded p-1 text-center text-white"
                          bind:value={set.actualWeight}
                          on:input={() => updateSetValue(mvIndex, setIndex, 'actualWeight', set.actualWeight ?? 0)}
                        />
                      </div>
                    </div>
                    <div class="text-right mt-2">
                      <button
                        on:click={() => resetSet(mvIndex, setIndex)}
                        class="text-xs text-red-300 hover:underline"
                      >
                        Reset
                      </button>
                    </div>
                  </li>
                {/each}
              </ul>

              <div class="mt-4">
                <label class="text-sm text-gray-300">Notes</label>
                <textarea
                  rows="2"
                  class="w-full bg-zinc-800 border border-zinc-700 mt-1 p-2 text-sm rounded text-white resize-none"
                  bind:value={movement.notes}
                />
              </div>
            </div>
          {/if}
        {/each}
      </div>
    {/if}

    <!-- Completed Section -->
    {#if session.movements?.some(m => m.isCompleted)}
      <div class="flex justify-between items-center mt-10 mb-4">
        <h2 class="text-xl text-white font-semibold">Completed Movements</h2>
        <button
          on:click={() => showCompleted = !showCompleted}
          class="text-white text-lg hover:text-gray-300"
        >
          {showCompleted ? '▾' : '▸'}
        </button>
      </div>

      {#if showCompleted}
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 opacity-70">
          {#each session.movements as movement, mvIndex (movement.movementID)}
            {#if movement.isCompleted}
              <div class="bg-zinc-800 border border-zinc-700 rounded-xl p-4 text-white">
                <h3 class="text-xl font-bold mb-2">{movement.movementBase?.name ?? 'Unnamed'}</h3>
                <p class="text-sm italic text-gray-400">Marked complete</p>
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

<!-- Floating Add Movement Button -->
<button
  on:click={() => showModal = true}
  class="fixed bottom-6 right-6 bg-white text-black hover:bg-gray-300 rounded-full w-12 h-12 text-2xl shadow-lg z-50"
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








