<script lang="ts">
  import { page } from "$app/stores";
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import { slugify } from "$lib/utils/slugify";
  import CreateMovementModal from "$lib/components/CreateMovement.svelte";
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
    MovementDTO,
    TrainingSessionDTO,
    TrainingProgramDTO,
    TrainingSessionStatus,
    WeightUnit,
  } from "$lib/api/ApiClient";
  import { base } from "$app/paths";
  // import { m } from 'vitest/dist/reporters-yx5ZTtEV.js';

  let slug = "";
  let sessionID = "";
  let session: TrainingSessionDTO;
  let program: TrainingProgramDTO;
  let unitMap: Record<number, "lbs" | "kg"> = {};
  let showUncompleted = true;
  let showCompleted = true;
  let showModal = false;
  let selectedDate = "";
  const allowedSteps = [1, 5, 10, 25];
  let weightStep: number = 5;
  let dateInput: HTMLInputElement;
  const baseUrl =
    typeof window !== "undefined"
      ? window.location.origin
      : "http://localhost:5174";

  function getSessionStatus(status: TrainingSessionStatus) {
    if (status === undefined) return "Unknown";
    const statusInt = status.valueOf();

    // Map the enum value to a string
    if (statusInt === TrainingSessionStatus._0) return "Planned";
    if (statusInt === TrainingSessionStatus._1) return "In Progress";
    if (statusInt === TrainingSessionStatus._2) return "Completed";
    if (statusInt === TrainingSessionStatus._3) return "Skipped";
  }

  onMount(async () => {
    slug = $page.params.slug;
    sessionID = $page.params.sessionID;

    const programsClient = new GetTrainingProgramsEndpointClient(baseUrl);
    const sessionClient = new GetTrainingSessionEndpointClient(baseUrl);

    try {
      const allPrograms = await programsClient.getAll3();
      const foundProgram = allPrograms.find(
        (p) => slugify(p.title ?? "") === slug,
      );
      if (!foundProgram) return;
      program = foundProgram;

      session = await sessionClient.get2(sessionID);
      selectedDate = session.date.toISOString().slice(0, 10);

      session.movements?.forEach((movement) => {
        movement.sets?.forEach((set) => {
          if (set.actualReps === 0) set.actualReps = set.recommendedReps;
          if (set.actualRPE === 0) set.actualRPE = set.recommendedRPE;
        });
      });

    } catch (err) {
      console.error("Failed to load session data:", err);
    }
  });

  function movementToUpdateRequest(movement: MovementDTO) {
    return {
      movementID: movement.movementID,
      trainingSessionID: movement.trainingSessionID!,
      movementBaseID: movement.movementBaseID,
      movementModifier: {
        name: movement.movementModifier.name,
        equipment: movement.movementModifier.equipment,
        duration: movement.movementModifier.duration,
      },
      weightUnit: movement.weightUnit,
      notes: movement.notes,
      isCompleted: movement.isCompleted,
    };
  }

  function getRpeColor(actual: number, recommended: number): string {
    if (actual === undefined || recommended === undefined)
      return "border-base-300";
    const diff = actual - recommended;

    if (diff >= 1.0) return "border-error text-error bg-error/10";
    if (diff <= -1.0) return "border-warning text-warning bg-warning/10";
    return "border-success text-success bg-success/10";
  }

  function updateSetValue(
    mvIndex: number,
    setIndex: number,
    field: keyof Pick<SetEntry, "actualReps" | "actualWeight" | "actualRPE">,
    value: number,
  ) {
    const set = session.movements[mvIndex].sets[setIndex];
    if (!set?.setEntryID || !set?.movementID) return;

    set[field] = value;

    const request = UpdateSetEntryRequest.fromJS({
      setEntryID: set.setEntryID!,
      movementID: set.movementID!,
      recommendedReps: set.recommendedReps ?? 5,
      recommendedWeight: set.recommendedWeight ?? 100,
      recommendedRPE: set.recommendedRPE ?? 7,
      actualReps: set.actualReps ?? 0,
      actualWeight: set.actualWeight ?? 0,
      actualRPE: set.actualRPE ?? 0,
    });

    const updateSetClient = new UpdateSetEntryEndpointClient(baseUrl);
    updateSetClient.update2(request).catch((err) => {
      console.error("Failed to update set entry", err);
    });
  }

  async function toggleComplete(index: number) {
    if (!session) return;

    const movement = session.movements[index];

    movement.isCompleted = !movement.isCompleted;

    const updateMovementClient = new UpdateMovementEndpointClient(baseUrl);
    await updateMovementClient.update(
      UpdateMovementRequest.fromJS(movementToUpdateRequest(movement)),
    );
    handleMovementsChanged();
  }

  async function deleteMovement(index: number) {
    if (!session) return;

    const movement = session.movements?.[index];
    if (!movement?.movementID) return;

    // Call your backend DELETE endpoint
    const res = await fetch(
      `${baseUrl}/api/movement/delete/${movement.movementID}`,
      {
        method: "DELETE",
        credentials: "include",
      },
    );

    if (!res.ok) {
      console.error("Failed to delete movement");
      return;
    }

    // Remove it from local session state
    session.movements = session.movements?.filter((_, i) => i !== index);
  }

  async function updateSession() {
    if (!session) return;

    const updateSessionClient = new UpdateTrainingSessionEndpointClient(
      baseUrl,
    );

    const updatedDate = new Date(selectedDate);
    updatedDate.setDate(updatedDate.getDate() + 1); // Increment by one day 
    session = await updateSessionClient.update4(
      UpdateTrainingSessionRequest.fromJS({
        trainingSessionID: session.trainingSessionID,
        trainingProgramID: session.trainingProgramID,
        date: updatedDate,
        status: session.status,
      }),
    );
    editingDate = false;
    await handleMovementsChanged();
  }

  async function handleMovementsChanged() {
    // Reload the session's movements
    const sessionClient = new GetTrainingSessionEndpointClient(baseUrl);
    session = await sessionClient.get2(sessionID);

    showModal = false;
  }

  const labelMap = {
    [WeightUnit._0]: "KG",
    [WeightUnit._1]: "LBS",
  };

  async function handleUnitToggle(movement: MovementDTO, e: Event) {
    const checked = (e.target as HTMLInputElement).checked;
    movement.weightUnit = checked ? WeightUnit._1 : WeightUnit._0;

    const updateMovementClient = new UpdateMovementEndpointClient(baseUrl);
    await updateMovementClient
      .update(UpdateMovementRequest.fromJS(movementToUpdateRequest(movement)))
      .catch((err) => {
        console.error("Failed to update movement weight unit", err);
      });
    await handleMovementsChanged();
  }



  let editingStatus = false;
  let tempStatus: TrainingSessionStatus;
  let editingDate = false;
  let tempDate: string; // Use string for input[type="date"] binding
</script>

{#if session}
  <div class="p-10 max-w-6xl mx-auto text-base-content">
    <a href={`/programs/${slug}`} class="btn btn-sm btn-outline mb-6">
      ← Back to Program
    </a>

    <div
      class="flex flex-col sm:flex-row sm:justify-between sm:items-center mb-6 gap-3"
    >
      <div class="flex flex-col gap-3">
        <!-- <div class="stats shadow border ">
          <div class="stat place-items-center">
            <div class="stat-title">{program?.title}</div>
            <div class="stat-value">Session # {session.sessionNumber}</div>
          </div>
        </div> -->
        <p class="text-4xl font-bold">{program?.title}</p>
        <p class="text-3xl italic">Session # {session.sessionNumber}</p>
        <div class="stats shadow border">
          <div class="stat">
            <div class="stat-title">Session Date</div>
            <div
              class="stat-value text-2xl font-normal"
              style="min-height:2.5rem;"
            >
              {#if editingDate}
                <input
                  type="date"
                  bind:value={selectedDate}
                  on:change={updateSession}
                  class="btn btn-accent"
                />
              {:else}
                {session.date.toISOString().slice(0, 10)}
              {/if}
            </div>
            <div class="stat-actions">
              {#if !editingDate}
                <button
                  class="btn btn-xs"
                  on:click={() => {
                    // Set tempDate to current session date in yyyy-mm-dd format
                    tempDate = session.date
                      ? new Date(session.date).toISOString().slice(0, 10)
                      : "";
                    editingDate = true;
                  }}
                >
                  EDIT
                </button>
              {/if}
            </div>
          </div>

          <div class="stat">
            <div class="stat-title">Session Status</div>
            <div
              class="stat-value text-2xl font-normal"
              style="min-height:2.5rem;"
            >
              {#if editingStatus}
                <select
                  class="select select-sm w-full"
                  bind:value={tempStatus}
                  on:change={() => {
                    /* no-op, handled on save */
                  }}
                >
                  <option value={TrainingSessionStatus._0}>Planned</option>
                  <option value={TrainingSessionStatus._1}>In Progress</option>
                  <option value={TrainingSessionStatus._2}>Completed</option>
                  <option value={TrainingSessionStatus._3}>Skipped</option>
                </select>
              {:else}
                {getSessionStatus(session.status)}
              {/if}
            </div>
            <div class="stat-actions">
              {#if editingStatus}
                <button
                  class="btn btn-xs btn-success mr-1"
                  on:click={async () => {
                    session.status = tempStatus;
                    await updateSession();
                    editingStatus = false;
                  }}
                >
                  Save
                </button>
                <button
                  class="btn btn-xs btn-ghost"
                  on:click={() => (editingStatus = false)}
                >
                  Cancel
                </button>
              {:else}
                <button
                  class="btn btn-xs"
                  on:click={() => {
                    tempStatus = session.status;
                    editingStatus = true;
                  }}
                >
                  EDIT
                </button>
              {/if}
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Uncompleted Section -->
    <div class="flex justify-between items-center mb-4">
      <h2 class="text-xl font-semibold">
        Uncompleted Movements
        <button
          on:click={() => (showUncompleted = !showUncompleted)}
          class="text-lg hover:text-primary ml-2"
        >
          {showUncompleted ? "▾" : "▸"}
        </button>
      </h2>
    </div>

    {#if showUncompleted}
      <div class="flex flex-wrap gap-6 items-start">
        {#each session.movements as movement, mvIndex (movement.movementID)}
          {#if !movement.isCompleted && !movement.notes?.startsWith("[REMOVED]")}
            <div
              class="bg-base-100 border border-base-300 rounded-xl p-5 shadow-md transition hover:shadow-lg w-full sm:w-[350px]"
            >
              <div class="flex justify-between items-start mb-3">
                <div>
                  <h3 class="text-xl font-bold">
                    {movement.movementBase?.name ?? "Unnamed"}
                  </h3>
                  <div class="badge badge-secondary">
                    {movement.movementModifier?.equipment}
                  </div>
                  <div class="badge badge-primary">
                    {movement.movementModifier?.name}
                  </div>
                  <div class="badge badge-primary">
                    Duration: {movement.movementModifier?.duration}
                  </div>
                </div>
              </div>

              <div class="flex flex-row items-center gap-2 mb-4">
                <p>Unit: {labelMap[movement.weightUnit]}</p>
                <input
                  type="checkbox"
                  checked={movement.weightUnit === WeightUnit._1}
                  class="toggle toggle-xs"
                  on:change={(e) => handleUnitToggle(movement, e)}
                />
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
                          on:input={() =>
                            updateSetValue(
                              mvIndex,
                              setIndex,
                              "actualReps",
                              set.actualReps ?? 0,
                            )}
                        />
                      </div>
                      <div class="flex flex-col gap-1">
                        <label class="text-base-content/60">RPE</label>
                        <input
                          type="number"
                          class={`input input-sm text-center ${getRpeColor(set.actualRPE, set.recommendedRPE)}`}
                          bind:value={set.actualRPE}
                          on:input={() =>
                            updateSetValue(
                              mvIndex,
                              setIndex,
                              "actualRPE",
                              set.actualRPE ?? 0,
                            )}
                        />
                      </div>
                      <div class="flex flex-col gap-1">
                        <label class="text-base-content/60">Weight</label>
                        <input
                          type="number"
                          step={weightStep}
                          class="input input-sm bg-base-100 border-base-300 text-center"
                          bind:value={set.actualWeight}
                          on:input={() =>
                            updateSetValue(
                              mvIndex,
                              setIndex,
                              "actualWeight",
                              set.actualWeight ?? 0,
                            )}
                        />
                      </div>
                    </div>
                    <!-- <div class="text-right mt-2">
                      <button
                        on:click={() => resetSet(mvIndex, setIndex)}
                        class="text-xs text-error hover:underline"
                      >
                        Reset
                      </button>
                    </div> -->
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

              <div class="flex gap-2">
                <button
                  on:click={() => toggleComplete(mvIndex)}
                  class="btn btn-xs btn-success"
                >
                  {movement.isCompleted ? "Undo" : "✓"}
                </button>
                <button
                  on:click={() => deleteMovement(mvIndex)}
                  class="btn btn-xs btn-error"
                >
                  X
                </button>
              </div>
            </div>
          {/if}
        {/each}
      </div>
    {/if}

    <!-- Completed Section -->
    {#if session.movements?.some((m) => m.isCompleted)}
      <div class="flex justify-between items-center mt-10 mb-4">
        <h2 class="text-xl font-semibold">Completed Movements</h2>
        <button
          on:click={() => (showCompleted = !showCompleted)}
          class="text-lg hover:text-primary"
        >
          {showCompleted ? "▾" : "▸"}
        </button>
      </div>

      {#if showCompleted}
        <div
          class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 opacity-70"
        >
          {#each session.movements as movement, mvIndex (movement.movementID)}
            {#if movement.isCompleted}
              <div class="bg-base-200 border border-base-300 rounded-xl p-4">
                <h3 class="text-xl font-bold mb-2">
                  {movement.movementBase?.name ?? "Unnamed"}
                </h3>
                <p class="text-sm italic text-base-content/60">
                  Marked complete
                </p>
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
  on:click={() => (showModal = true)}
  class="fixed bottom-6 right-6 btn btn-primary btn-circle text-xl shadow-lg z-50"
>
  +
</button>

{#if showModal && session?.trainingSessionID}
  <CreateMovementModal
    show={showModal}
    sessionID={session.trainingSessionID}
    on:close={() => (showModal = false)}
    on:created={handleMovementsChanged}
  />
{/if}
