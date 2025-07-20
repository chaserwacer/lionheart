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
    UpdateMovementOrderEndpointClient,
    UpdateMovementOrderRequest,
    GetTrainingProgramEndpointClient,
    DeleteTrainingSessionEndpointClient,
    GetEquipmentsEndpointClient,
    Equipment,
  } from "$lib/api/ApiClient";
  import { base } from "$app/paths";

  let slug = $page.params.slug;
  let sessionID = $page.params.sessionID;
  let session: TrainingSessionDTO;
  let program: TrainingProgramDTO;
  let showModal = false;
  let selectedDate = "";
  let editingMovementBaseName = false;

  let chosenMovementBaseIndexForEditing = 0;
  let modifiers: MovementBase[] = [];
  let equipmentOptions: Equipment[] = [];
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
    if (statusInt === TrainingSessionStatus._2) return "Complete";
    if (statusInt === TrainingSessionStatus._3) return "Skipped";
  }

  onMount(async () => {
    const programsClient = new GetTrainingProgramEndpointClient(baseUrl);
    const sessionClient = new GetTrainingSessionEndpointClient(baseUrl);
    const movementBaseClient = new GetMovementBasesEndpointClient(baseUrl);
    const equipmentClient = new GetEquipmentsEndpointClient(baseUrl);

    try {
      program = await programsClient.get(slug);
      session = await sessionClient.get2(slug, sessionID);
      if (!session) return;

      selectedDate = session.date.toISOString().slice(0, 10);
      modifiers = await movementBaseClient.getAll2();
      equipmentOptions = await equipmentClient.getAll();
      console.log("equipmentOptions", equipmentOptions);
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
    movement.movementBaseID = movement.movementBase.movementBaseID!;
    editingMovementBaseName = false;
    const equipment = equipmentOptions.find(e => e.equipmentID === movement.movementModifier.equipmentID);

    const request = UpdateMovementRequest.fromJS({
      movementID: movement.movementID,
      trainingSessionID: movement.trainingSessionID!,
      movementBaseID: movement.movementBaseID,
      movementModifier: {
        name: movement.movementModifier.name,
        equipmentID: equipment?.equipmentID,
        equipment: equipment,
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

  async function deleteMovement(movement: MovementDTO) {
    if (!session) return;
    if (!movement?.movementID) return;
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
    await refreshSessionData();
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
    await client.create4(request).catch((err) => {
      console.error("Failed to add set to movement", err);
    });

    await refreshSessionData();
  }

  async function refreshSessionData() {
    // Reload the session's movements
    const sessionClient = new GetTrainingSessionEndpointClient(baseUrl);

    session = await sessionClient.get2(slug, sessionID);
    showModal = false;

    const movementBaseClient = new GetMovementBasesEndpointClient(baseUrl);
    const equipmentClient = new GetEquipmentsEndpointClient(baseUrl);

 
    modifiers = await movementBaseClient.getAll2();
    equipmentOptions = await equipmentClient.getAll();
    
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
    await client.delete4(set.setEntryID).catch((err) => {
      console.error("Failed tdelete set entry", err);
    });

    await refreshSessionData();
  }

  async function deleteSession() {
    const confirmed = confirm("Are you sure you want to delete this session?");
    if (!confirmed || !session) return;
    const deleteClient = new DeleteTrainingSessionEndpointClient(baseUrl);
    try {
      await deleteClient.delete6(session.trainingSessionID);
      goto(`/programs/${slug}`); // Redirect to program page after deletion
    } catch {
      alert("Failed to delete session.");
    }
  }
  async function reorderMovement(
    movement: MovementDTO,
    direction: "up" | "down",
  ) {
    if (!session || !session.movements) return;
    // Sort movements by ordering
    const movements = session.movements
      .slice()
      .sort((a, b) => a.ordering - b.ordering);
    const index = movements.findIndex(
      (m) => m.movementID === movement.movementID,
    );
    if (
      (direction === "up" && index === 0) ||
      (direction === "down" && index === movements.length - 1)
    ) {
      return;
    }
    const swapWith = direction === "up" ? index - 1 : index + 1;
    [movements[index], movements[swapWith]] = [
      movements[swapWith],
      movements[index],
    ];
    // Assign new ordering values
    const movementOrderUpdates = movements.map((m, i) => ({
      movementID: m.movementID,
      ordering: i,
    }));
    try {
      const client = new UpdateMovementOrderEndpointClient(baseUrl);
      await client.updateOrder(
        UpdateMovementOrderRequest.fromJS({
          trainingSessionID: session.trainingSessionID,
          movements: movementOrderUpdates,
        }),
      );
    } catch (err) {
      console.error("Failed to update order:", err);
    }
    await refreshSessionData();
  }
</script>

{#if session}
  <div class="p-5 pt-2 max-w-6xl mx-auto text-base-content">
    <div class="">
      <div class="flex justify-between items-center w-full">
        <a href={`/programs/${slug}`} class="btn btn-sm btn-primary">
          ← Back
        </a>
        <h1 class="text-xl md:text-4xl font-extrabold mb-2 text-center">
          {program.title}
        </h1>
        <a
          class="btn btn-sm btn-primary"
          href={`/movementLib?returnTo=/programs/${slug}/session/${sessionID}`}
        >
          Items →
        </a>
      </div>

      <div class="text-center items-center justify-center">
        {#each program.tags as tag}
          <span class="badge badge-primary">{tag}</span>
        {/each}
      </div>
    </div>
    <div class="divider divider-2 divider-primary"></div>

    <div
      class="flex flex-col sm:flex-row sm:justify-between sm:items-center mb-6 gap-5"
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
                  <option value={TrainingSessionStatus._2}>Complete</option>
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
      {#each session.movements
        ?.slice()
        .sort((a, b) => a.ordering - b.ordering) as movement}
        {#if !movement.isCompleted}
          <div
            class="bg-base-100 border border-base-300 rounded-xl p-5 shadow-md transition hover:shadow-lg w-full"
          >
            <div class="flex justify-between items-start mb-1 flex-col">
              <div class="flex flex-wrap gap-x-3 items-center">
                {#if editingMovementBaseName && chosenMovementBaseIndexForEditing === movement.ordering}
                  <select
                    bind:value={movement.movementBase}
                    on:change={() => {
                      updateMovement(movement);
                    }}
                    class="select select-primary select-md"
                  >
                    {#each modifiers as mod}
                      <option value={mod}>{mod.name}</option>
                    {/each}
                  </select>
                  <button
                    class="btn btn-xs ml-2"
                    on:click={() => (editingMovementBaseName = false)}
                    aria-label="Cancel">✕</button
                  >
                {:else}
                  <span class="font-bold text-4xl mb-2 font-primary"
                    >{movement.movementBase.name}</span
                  >
                  <button
                    type="button"
                    class="btn btn-xs ml-2"
                    on:click={() => (
                      (editingMovementBaseName = true),
                      (chosenMovementBaseIndexForEditing = movement.ordering)
                    )}
                    aria-label="Edit movement base">✎</button
                  >
                {/if}
              </div>
              <div class="flex">
                <input
                  type="text"
                  placeholder="Modifer"
                  class="input input-sm input-primary w-28 m-1"
                  bind:value={movement.movementModifier.name}
                  on:change={() => updateMovement(movement)}
                />

                <select
                  bind:value={movement.movementModifier.equipmentID}
                  on:change={() => updateMovement(movement)}
                  class="select select-sm select-primary m-1"
                >
                
                  {#each equipmentOptions as e}
                  {#if e.equipmentID === movement.movementModifier.equipmentID}
                    <option value={e.equipmentID} selected>{e.name}</option>
                  {:else}
                    <option value={e.equipmentID}>{e.name}</option>
                  {/if}
                  {/each}
                </select>

                <input
                  type="number"
                  id="duration"
                  min="0"
                  bind:value={movement.movementModifier.duration}
                  on:input={() => updateMovement(movement)}
                  class="peer input input-sm input-primary w-16 m-1"
                />

                <label
                  class="swap border border-primary rounded w-10 m-1 swap-xs swap-flip m-1"
                >
                  <input
                    type="checkbox"
                    checked={movement.weightUnit === WeightUnit._1}
                    on:change={(e) => handleUnitToggle(movement, e)}
                  />

                  <div class="swap-on text-xs">LBS</div>

                  <div class="swap-off text-xs">KGS</div>
                </label>
              </div>
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
                      <td class="relative group">
                        <input
                          type="number"
                          class="input input-xs bg-base-200 border-base-300 text-center w-12"
                          min="0"
                          bind:value={set.recommendedReps}
                          on:input={() => updateSetEntry(set)}
                        />
                      </td>
                      <td class="relative group">
                        <input
                          type="number"
                          min="0"
                          class="input input-xs bg-base-200 border-base-300 text-center w-16"
                          bind:value={set.recommendedWeight}
                          on:input={() => updateSetEntry(set)}
                        />
                      </td>
                      <td class="relative group">
                        <input
                          type="number"
                          class="input input-xs bg-base-200 border-base-300 text-center w-12"
                          min="0"
                          max="10"
                          step="0.5"
                          bind:value={set.recommendedRPE}
                          on:input={() => updateSetEntry(set)}
                        />
                      </td>
                      <td class="relative group">
                        <!-- Button appears only on hover -->
                        <button
                          class="opacity-0 group-hover:opacity-100 transition-opacity duration-200 h-px w-3"
                          on:click={() => {
                            set.actualReps = set.recommendedReps;
                            updateSetEntry(set);
                          }}
                        >
                          ←
                        </button>
                        <input
                          type="number"
                          class="input input-xs bg-base-100 border-base-300 text-center w-12"
                          min="0"
                          bind:value={set.actualReps}
                          on:input={() => updateSetEntry(set)}
                        />
                      </td>
                      <td class="relative group">
                        <!-- Button appears only on hover -->
                        <button
                          class="opacity-0 group-hover:opacity-100 transition-opacity duration-200 h-px w-3"
                          on:click={() => {
                            set.actualWeight = set.recommendedWeight;
                            updateSetEntry(set);
                          }}
                        >
                          ←
                        </button>
                        <input
                          type="number"
                          class="input input-xs bg-base-100 border-base-300 text-center w-16"
                          min="0"
                          bind:value={set.actualWeight}
                          on:input={() => updateSetEntry(set)}
                        />
                      </td>
                      <td class="relative group">
                        <!-- Button appears only on hover -->
                        <button
                          class="opacity-0 group-hover:opacity-100 transition-opacity duration-200 h-px w-3"
                          on:click={() => {
                            set.actualRPE = set.recommendedRPE;
                            updateSetEntry(set);
                          }}
                        >
                          ←
                        </button>
                        <input
                          type="number"
                          class="input input-xs bg-base-100 border-base-300 text-center w-12"
                          bind:value={set.actualRPE}
                          min="0"
                          max="10"
                          step="0.5"
                          on:input={() => updateSetEntry(set)}
                        />
                      </td>
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
                on:click={() => deleteMovement(movement)}
                class="btn btn-xs btn-error"
              >
                X
              </button>
              <!-- Up arrow -->
              {#if movement.ordering > 0}
                <button
                  class="btn btn-xs"
                  on:click={() => reorderMovement(movement, "up")}
                >
                  ↑
                </button>
              {/if}
              <!-- Down arrow -->
              {#if movement.ordering < session.movements.length - 1}
                <button
                  class="btn btn-xs"
                  on:click={() => reorderMovement(movement, "down")}
                >
                  ↓
                </button>
              {/if}
            </div>
          </div>
        {/if}
        {#if movement.isCompleted}
          <div
            class="bg-base-200 border border-base-300 rounded-xl p-5 shadow-md transition hover:shadow-lg w-full"
          >
            <div class="font-bold text-2xl">{movement.movementBase.name}</div>
            <div class="badge badge-primary">
              {movement.movementModifier.name}
            </div>
            <div class="badge badge-primary">
              {movement.movementModifier.equipment}
            </div>
            <div class="badge badge-primary">
              {movement.movementModifier.duration}s
            </div>
            <div class="badge badge-primary">
              {labelMap[movement.weightUnit]}
            </div>

            <div>
              <table class="table table-xs w-1/2 outline outline-1 mt-3">
                <thead>
                  <tr>
                    <th>Set</th>
                    <th>Reps</th>
                    <th>Weight</th>
                    <th>RPE</th>
                  </tr>
                </thead>
                <tbody>
                  {#each movement.sets as set, index}
                    <tr>
                      <td>{index + 1}</td>
                      <td>{set.actualReps}</td>
                      <td>{set.actualWeight}</td>
                      <td>{set.actualRPE}</td>
                    </tr>
                  {/each}
                </tbody>
              </table>
            </div>
            <div class="mt-2 whitespace-pre-wrap">Notes: {movement.notes}</div>
            <button
              class="btn btn-xs outline mt-2"
              on:click={() => toggleMovementComplete(movement)}
            >
              Edit Movement
            </button>
          </div>
        {/if}
      {/each}
      <button class="btn btn-error" on:click={deleteSession}
        >Delete Training Session</button
      >
    </div>
  </div>
{:else}
  <div class="p-6 max-w-4xl mx-auto flex items-center justify-center">
    <span class="loading loading-spinner loading-xs"></span>
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
