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

function getRpeColor(actual?: number, recommended?: number): string {
  if (actual === undefined || recommended === undefined) return 'border-base-300';
  const diff = actual - recommended;

  if (diff >= 1.0) return 'border-error text-error bg-error/10';
  if (diff <= -1.0) return 'border-warning text-warning bg-warning/10';
  return 'border-success text-success bg-success/10';
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

    await updateSessionStatus();
  }


  async function deleteMovement(index: number) {
  if (!session) return;

  const movement = session.movements?.[index];
  if (!movement?.movementID) return;

  // Call your backend DELETE endpoint
  const res = await fetch(`${baseUrl}/api/movement/delete/${movement.movementID}`, {
    method: 'DELETE',
    credentials: 'include'
  });

  if (!res.ok) {
    console.error('Failed to delete movement');
    return;
  }

  // Remove it from local session state
  session.movements = session.movements?.filter((_, i) => i !== index) ?? [];

  await updateSessionStatus();
}

  async function updateSessionStatus() {
    if (!session) return;

    const movements = session.movements ?? [];
    const isComplete = movements.length === 0 || movements.every(m => m.isCompleted);

    session.status = isComplete
      ? TrainingSessionStatus._2
      : TrainingSessionStatus._0;

    const updateSessionClient = new UpdateTrainingSessionEndpointClient(baseUrl);
    await updateSessionClient.update4(UpdateTrainingSessionRequest.fromJS({
      trainingSessionID: session.trainingSessionID!,
      trainingProgramID: session.trainingProgramID!,
      date: session.date!,
      status: session.status
    }));
  }

  async function handleMovementCreated() {
    // Reload the session's movements
    const sessionClient = new GetTrainingSessionEndpointClient(baseUrl);
    session = await sessionClient.get2(sessionID);

    await updateSessionStatus();
    showModal = false;
  }


  function convertWeight(w: number, toUnit: 'kg' | 'lbs'): number {
    return toUnit === 'kg'
      ? +(w * 0.453592).toFixed(1)
      : +(w / 0.453592).toFixed(1);
  }

</script>

{#if session}
  <div class="p-10 max-w-6xl mx-auto text-base-content">
    <a
      href={`/programs/${slug}`}
      class="btn btn-sm btn-outline mb-6"
    >
      ← Back to Program
    </a>

    <div class="flex flex-col sm:flex-row sm:justify-between sm:items-center mb-6 gap-3">
      <h1 class="text-3xl sm:text-4xl font-bold">
        Session {sessionIndex + 1} <span class="text-base-content/60">· {program?.title}</span>
      </h1>
      <div class="flex items-center gap-2">
        <label class="text-sm">Weight step:</label>
        <select bind:value={weightStep} class="select select-sm bg-base-200 text-base-content border-base-300">
          {#each allowedSteps as step}
            <option value={step}>{step}</option>
          {/each}
        </select>
      </div>
    </div>

    <!-- Uncompleted Section -->
    <div class="flex justify-between items-center mb-4">
      <h2 class="text-xl font-semibold">Uncompleted Movements
        <button
          on:click={() => showUncompleted = !showUncompleted}
          class="text-lg hover:text-primary ml-2"
        >
          {showUncompleted ? '▾' : '▸'}
        </button>
      </h2>
    </div>

    {#if showUncompleted}
      <div class="flex flex-wrap gap-6 items-start">
        {#each session.movements ?? [] as movement, mvIndex (movement.movementID)}
          {#if !movement.isCompleted && !movement.notes?.startsWith('[REMOVED]')}
            <div class="bg-base-100 border border-base-300 rounded-xl p-5 shadow-md transition hover:shadow-lg w-full sm:w-[350px]">

              <div class="flex justify-between items-start mb-3">
                <div>
                  <h3 class="text-xl font-bold">{movement.movementBase?.name ?? 'Unnamed'}</h3>
                  <p class="text-sm text-base-content/60 italic">{movement.movementModifier?.name ?? 'No Modifier'}</p>
                </div>
                <span class="text-sm text-warning">💡 Focus</span>
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
                    class="btn btn-xs btn-success"
                  >
                    {movement.isCompleted ? 'Undo' : 'Complete'}
                  </button>
                  <button
                    on:click={() => deleteMovement(mvIndex)}
                    class="btn btn-xs btn-error"
                  >
                    Remove
                  </button>
                </div>
              </div>

              <ul class="space-y-3">
                {#each movement.sets ?? [] as set, setIndex}
                  <li class="bg-base-200 border border-base-300 p-3 rounded">
                    <div class="grid grid-cols-3 gap-4 text-sm">
                      <div class="flex flex-col gap-1">
                        <label class="text-base-content/60">Reps</label>
                        <input
                          type="number"
                          class="input input-sm bg-base-100 border-base-300 text-center"
                          bind:value={set.actualReps}
                          on:input={() => updateSetValue(mvIndex, setIndex, 'actualReps', set.actualReps ?? 0)}
                        />
                      </div>
                      <div class="flex flex-col gap-1">
                        <label class="text-base-content/60">RPE</label>
                        <input
                          type="number"
                          class={`input input-sm text-center ${getRpeColor(set.actualRPE, set.recommendedRPE)}`}
                          bind:value={set.actualRPE}
                          on:input={() => updateSetValue(mvIndex, setIndex, 'actualRPE', set.actualRPE ?? 0)}
                        />
                      </div>
                      <div class="flex flex-col gap-1">
                        <label class="text-base-content/60">Weight</label>
                        <input
                          type="number"
                          step={weightStep}
                          class="input input-sm bg-base-100 border-base-300 text-center"
                          bind:value={set.actualWeight}
                          on:input={() => updateSetValue(mvIndex, setIndex, 'actualWeight', set.actualWeight ?? 0)}
                        />
                      </div>
                    </div>
                    <div class="text-right mt-2">
                      <button
                        on:click={() => resetSet(mvIndex, setIndex)}
                        class="text-xs text-error hover:underline"
                      >
                        Reset
                      </button>
                    </div>
                  </li>
                {/each}
              </ul>

              <div class="mt-4">
                <label class="text-sm">Notes</label>
                <textarea
                  rows="2"
                  class="textarea textarea-sm bg-base-200 border-base-300 mt-1 text-sm resize-none w-full"
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
        <h2 class="text-xl font-semibold">Completed Movements</h2>
        <button
          on:click={() => showCompleted = !showCompleted}
          class="text-lg hover:text-primary"
        >
          {showCompleted ? '▾' : '▸'}
        </button>
      </div>

      {#if showCompleted}
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 opacity-70">
          {#each session.movements as movement, mvIndex (movement.movementID)}
            {#if movement.isCompleted}
              <div class="bg-base-200 border border-base-300 rounded-xl p-4">
                <h3 class="text-xl font-bold mb-2">{movement.movementBase?.name ?? 'Unnamed'}</h3>
                <p class="text-sm italic text-base-content/60">Marked complete</p>
                <button
                  on:click={() => toggleComplete(mvIndex)}
                  class="text-xs text-warning hover:underline mt-2"
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
  <div class="p-6 max-w-4xl mx-auto text-error">
    <h1 class="text-2xl font-bold">Session not found</h1>
  </div>
{/if}

<!-- Floating Add Movement Button -->
<button
  on:click={() => showModal = true}
  class="fixed bottom-6 right-6 btn btn-primary btn-circle text-xl shadow-lg z-50"
>
  +
</button>

{#if showModal && session?.trainingSessionID}
  <CreateMovementModal
    show={showModal}
    sessionID={session.trainingSessionID}
    on:close={() => showModal = false}
    on:created={handleMovementCreated}
  />
{/if}
