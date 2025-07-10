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
    CreateSetEntryRequest,
    CreateSetEntryEndpointClient,
    UpdateMovementRequest,
    UpdateTrainingSessionRequest,
    SetEntry,
    MovementDTO,
    TrainingSessionDTO,
    TrainingProgramDTO,
    TrainingSessionStatus,
    WeightUnit,
    SetEntryDTO,
    DeleteSetEntryEndpointClient,
    MovementBase,
    GetMovementBasesEndpointClient,
  } from "$lib/api/ApiClient";
  import { base } from "$app/paths";

  let slug = "";
  let sessionID = "";
  let session: TrainingSessionDTO;
  let program: TrainingProgramDTO;
  let showUncompleted = true;
  let showCompleted = true;
  let showModal = false;
  let selectedDate = "";
  let editingMovementBaseName = false;
  let editingSetViewer = false;
  let weightStep: number = 5;
  let dateInput: HTMLInputElement;
  let windowWidth = 0;
  let modifiers: MovementBase[] = [];
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
    const movementBaseClient = new GetMovementBasesEndpointClient(baseUrl);

    try {
      const allPrograms = await programsClient.getAll3();
      const foundProgram = allPrograms.find(
        (p) => slugify(p.title ?? "") === slug,
      );
      if (!foundProgram) return;
      program = foundProgram;

      session = await sessionClient.get2(sessionID);
      selectedDate = session.date.toISOString().slice(0, 10);

      modifiers = await movementBaseClient.getAll();
    } catch (err) {
      console.error("Failed to load session data:", err);
    }
  });

  async function updateSetEntry(set: SetEntry) {
    const request = UpdateSetEntryRequest.fromJS({
      setEntryID: set.setEntryID,
      movementID: set.movementID,
      recommendedReps: set.recommendedReps,
      recommendedWeight: set.recommendedWeight,
      recommendedRPE: set.recommendedRPE,
      actualReps: set.actualReps,
      actualWeight: set.actualWeight,
      actualRPE: set.actualRPE,
    });

    const updateSetClient = new UpdateSetEntryEndpointClient(baseUrl);
    await updateSetClient.update2(request).catch((err) => {
      console.error("Failed to update set entry", err);
    });
  }

  async function updateMovement(movement: MovementDTO) {
    editingMovementBaseName = false;
    const request = UpdateMovementRequest.fromJS({
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
    });

    var client = new UpdateMovementEndpointClient(baseUrl);
    await client.update(request).catch((err) => {
      console.error("Failed to update movement", err);
    });
  }

  async function toggleMovementComplete(movement: MovementDTO) {
    movement.isCompleted = !movement.isCompleted;

    await updateMovement(movement);
    await refreshSessionData();
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
    await refreshSessionData();
  }

  async function addSetEntryToMovement(movement: MovementDTO) {
    const request = CreateSetEntryRequest.fromJS({
      movementID: movement.movementID,
      actualRPE: 0,
      actualWeight: 0,
      actualReps: 0,
      recommendedReps: 0,
      recommendedWeight: 0,
      recommendedRPE: 0,
    });

    var client = new CreateSetEntryEndpointClient(baseUrl);
    await client.create3(request).catch((err) => {
      console.error("Failed to add set to movement", err);
    });

    await refreshSessionData();
  }

  async function refreshSessionData() {
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

    await updateMovement(movement);
    await refreshSessionData();
  }

  let editingStatus = false;
  let tempStatus: TrainingSessionStatus;
  let editingDate = false;
  let tempDate: string; // Use string for input[type="date"] binding

  async function deleteSetEntry(set: SetEntryDTO) {
    var client = new DeleteSetEntryEndpointClient(baseUrl);
    await client.delete3(set.setEntryID).catch((err) => {
      console.error("Failed tdelete set entry", err);
    });

    await refreshSessionData();
  }
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

    <div class="flex flex-col gap-6 items-stretch w-full">
      {#each session.movements as movement, mvIndex (movement.movementID)}
        {#if !movement.isCompleted}
          <div
            class="bg-base-100 border border-base-300 rounded-xl p-5 shadow-md transition hover:shadow-lg w-full"
          >
            <div class="flex justify-between items-start mb-3 flex flex-col">
              <div class="flex flex-wrap gap-x-5">
                <button
                  type="button"
                  on:click={() => (editingMovementBaseName = true)}
                  class="font-bold text-5xl mb-5 font-primary bg-transparent border-none text-left p-0 focus:outline-none"
                  style="background: none;"
                >
                  {movement.movementBase.name}
                </button>
                {#if editingMovementBaseName}
                  <select
                    bind:value={movement.movementBase}
                    on:change={() => {
                      updateMovement(movement);
                    }}
                    class="select select-primary select-xs mt-4"
                  >
                    {#each modifiers as mod}
                      <option value={mod}>{mod.name}</option>
                    {/each}
                  </select>
                {/if}
              </div>
              <div class="flex">
                <input
                  type="text"
                  placeholder="Type here"
                  class="input input-primary w-32 m-1"
                  bind:value={movement.movementModifier.name}
                  on:change={() => updateMovement(movement)}
                />

                <select
                  class="select select-primary m-1"
                  bind:value={movement.movementModifier.equipment}
                  on:change={() => updateMovement(movement)}
                >
                  <option disabled selected
                    >{movement.movementModifier.equipment}</option
                  >
                  <option>Kettlebell</option>
                  <option>Barbell</option>
                  <option>Angular</option>
                </select>

                <input
                  type="number"
                  id="duration"
                  min="0"
                  bind:value={movement.movementModifier.duration}
                  on:input={() => updateMovement(movement)}
                  class="peer input input-xl input-primary w-16 m-1"
                />
              </div>
            </div>

            <div class="flex flex-row items-center m-1 mb-4">
              <p>Unit: {labelMap[movement.weightUnit]}</p>
              <input
                type="checkbox"
                checked={movement.weightUnit === WeightUnit._1}
                class=" ml-1 toggle toggle-xs"
                on:change={(e) => handleUnitToggle(movement, e)}
              />
            </div>

            <ul
              class="overflow-x-auto w-full border border-base-300 rounded-xl shadow-md hover:shadow-lg transition p-4"
            >
              <table class="table table-sm w-full h-full max-w-full max-h-full">
                <thead>
                  <tr class="hidden md:table-row font-bold text-lg">
                    <th></th>
                    <th class="italic">Target</th>
                    <th></th>
                    <th></th>
                    <th>Actual</th>
                  </tr>
                  <tr class="hidden md:table-row">
                    <!-- Show all headers in one row on medium+ screens -->
                    <th>SET</th>
                    <th class="italic">REPS</th>
                    <th class="italic">WEIGHT</th>
                    <th class="italic">RPE</th>
                    <th>REPS</th>
                    <th>WEIGHT</th>
                    <th>RPE</th>
                    <th></th>
                  </tr>
                  <tr class="md:hidden sm:table-row">
                    <!-- Show all headers in one row on medium+ screens -->
                    <th>SET</th>
                    <th>TYPE</th>
                    <th>REPS</th>
                    <th>WEIGHT</th>
                    <th>RPE</th>
                  </tr>
                </thead>
                <tbody>
                  {#each movement.sets as set, index}
                    <!-- Full single-row layout for medium+ screens -->
                    <tr class="hidden md:table-row">
                      <td class="font text-sm font-bold w-fit">{index + 1}</td>
                      <td
                        ><input
                          type="number"
                          class="input input-xs bg-base-200 border-base-300 text-center w-12"
                          min="0"
                          bind:value={set.recommendedReps}
                          on:input={() => updateSetEntry(set)}
                        /></td
                      >
                      <td
                        ><input
                          type="number"
                          min="0"
                          class="input input-xs bg-base-200 border-base-300 text-center w-20"
                          bind:value={set.recommendedWeight}
                          on:input={() => updateSetEntry(set)}
                        /></td
                      >
                      <td
                        ><input
                          type="number"
                          class="input input-xs bg-base-200 border-base-300 text-center w-12"
                          min="0"
                          max="10"
                          step="0.5"
                          bind:value={set.recommendedRPE}
                          on:input={() => updateSetEntry(set)}
                        /></td
                      >
                      <td class=" "
                        ><input
                          type="number"
                          class="input input-xs bg-base-100 border-base-300 text-center w-12"
                          min="0"
                          bind:value={set.actualReps}
                          on:input={() => updateSetEntry(set)}
                        /></td
                      >
                      <td
                        ><input
                          type="number"
                          class="input input-xs bg-base-100 border-base-300 text-center w-20"
                          min="0"
                          bind:value={set.actualWeight}
                          on:input={() => updateSetEntry(set)}
                        /></td
                      >
                      <td
                        ><input
                          type="number"
                          class="input input-xs bg-base-100 border-base-300 text-center w-12"
                          min="0"
                          max="10"
                          step="0.5"
                          bind:value={set.actualRPE}
                          on:input={() => updateSetEntry(set)}
                        /></td
                      >
                      <td
                        ><button
                          class="btn btn-xs btn-error"
                          on:click={() => deleteSetEntry(set)}>X</button
                        ></td
                      >
                    </tr>
                    <!-- SMALL SCREEN VERSION -->
                    <tr class="md:hidden border-none">
                      <td class="font text-sm font-bold">{index + 1}</td>
                      <td class="text-xs">TARGET</td>
                      <td
                        ><input
                          type="number"
                          class="input input-xs bg-base-200 border-base-300 text-center w-12"
                          min="0"
                          bind:value={set.recommendedReps}
                          on:input={() => updateSetEntry(set)}
                        /></td
                      >
                      <td
                        ><input
                          type="number"
                          class="input input-xs bg-base-200 border-base-300 text-center w-16"
                          min="0"
                          bind:value={set.recommendedWeight}
                          on:input={() => updateSetEntry(set)}
                        /></td
                      >
                      <td
                        ><input
                          type="number"
                          min="0"
                          max="10"
                          step="0.5"
                          class="input input-xs bg-base-200 border-base-300 text-center w-12"
                          bind:value={set.recommendedRPE}
                          on:input={() => updateSetEntry(set)}
                        /></td
                      >
                    </tr>
                    <tr class="md:hidden hidden:lg">
                      <td></td>
                      <td class="text-xs">ACTUAL</td>
                      <td class=" "
                        ><input
                          type="number"
                          class="input input-xs bg-base-100 border-base-300 text-center w-12"
                          min="0"
                          bind:value={set.actualReps}
                          on:input={() => updateSetEntry(set)}
                        /></td
                      >

                      <td
                        ><input
                          type="number"
                          class="input input-xs bg-base-100 border-base-300 text-center w-16"
                          min="0"
                          bind:value={set.actualWeight}
                          on:input={() => updateSetEntry(set)}
                        /></td
                      >
                      <td
                        ><input
                          type="number"
                          class="input input-xs bg-base-100 border-base-300 text-center w-12"
                          bind:value={set.actualRPE}
                          min="0"
                          max="10"
                          step="0.5"
                          on:input={() => updateSetEntry(set)}
                        /></td
                      >
                      <td
                        ><button
                          class="btn btn-xs btn-error"
                          on:click={() => deleteSetEntry(set)}>X</button
                        ></td
                      >
                    </tr>
                  {/each}
                </tbody>
              </table>

              <button
                on:click={() => addSetEntryToMovement(movement)}
                class="text-sm text-green-500 hover:underline mt-2"
                >+ Add Set</button
              >
            </ul>

            <div class="mt-4">
              <p class="text-sm">Notes</p>
              <textarea
                rows="2"
                class="textarea textarea-sm bg-base-200 border-base-300 mt-1 text-sm resize-none w-full"
                bind:value={movement.notes}
                on:change={() => updateSetEntry(movement)}
              />
            </div>

            <div class="flex gap-2">
              <button
                on:click={() => toggleMovementComplete(movement)}
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
                  {movement.movementBase.name}
                </h3>
                <p class="text-sm italic text-base-content/60">
                  Marked complete
                </p>
                <button
                  on:click={() => toggleMovementComplete(movement)}
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
    on:created={refreshSessionData}
  />
{/if}
