<script lang="ts">
  import { onMount } from "svelte";
  import {
    InjuryDTO,
    CreateInjuryRequest,
    CreateInjuryEventRequest,
    AddInjuryEventWrapper,
    AddInjuryEventEndpointClient,
    CreateInjuryEndpointClient,
    GetUserInjuriesEndpointClient,
    MarkInjuryResolvedEndpointClient,
    InjuryEventType,
    GetTrainingProgramsEndpointClient,
    TrainingSessionStatus,
    TrainingSessionDTO,
    TrainingProgramDTO
  } from "$lib/api/ApiClient";
  import { writable } from "svelte/store";

  const baseUrl = typeof window !== "undefined" ? window.location.origin : "http://localhost:5174";

  let injuries = writable<InjuryDTO[]>([]);
  let selectedInjury: InjuryDTO | null = null;
  let newCategory = "";
  let newDate = new Date().toISOString().slice(0, 10);
  let newEventNotes = "";
  let newEventPain = 0;
  let trainingSessions: TrainingSessionDTO[] = [];
  let programs: TrainingProgramDTO[] = [];
  let selectedSession: TrainingSessionDTO | null = null;
  let selectedProgram: TrainingProgramDTO | null = null;

  let showAddModal = false;
  let showEventModal = false;
  let showSessionPicker = false;
  let pickingFor = "injury"; // or "event"

  async function loadTrainingSessions() {
    const client = new GetTrainingProgramsEndpointClient(baseUrl);
    programs = await client.getAll4();
    trainingSessions = programs.flatMap(p => p.trainingSessions ?? []);
  }

  async function loadInjuries() {
    const client = new GetUserInjuriesEndpointClient(baseUrl);
    injuries.set(await client.getUserInjuries());
  }

  async function createInjuryWithFirstEvent() {
    const injuryClient = new CreateInjuryEndpointClient(baseUrl);
    const request = new CreateInjuryRequest({ category: newCategory, injuryDate: new Date(newDate) });
    const injury = await injuryClient.create2(request);

    if (selectedSession) {
      const eventRequest = new CreateInjuryEventRequest({
        trainingSessionID: selectedSession.trainingSessionID,
        notes: newEventNotes,
        painLevel: newEventPain,
        injuryType: InjuryEventType._0
      });

      const wrapper = new AddInjuryEventWrapper({ injuryId: injury.injuryID, request: eventRequest });
      const eventClient = new AddInjuryEventEndpointClient(baseUrl);
      await eventClient.addEvent(wrapper);
    }

    newCategory = "";
    newEventNotes = "";
    newEventPain = 0;
    newDate = new Date().toISOString().slice(0, 10);
    selectedSession = null;
    showAddModal = false;

    await loadInjuries();
  }

  async function addEventToInjury() {
    if (!selectedInjury || !selectedSession) return;

    const eventRequest = new CreateInjuryEventRequest({
      trainingSessionID: selectedSession.trainingSessionID,
      notes: newEventNotes,
      painLevel: newEventPain,
      injuryType: InjuryEventType._0
    });

    const wrapper = new AddInjuryEventWrapper({ injuryId: selectedInjury.injuryID, request: eventRequest });
    const eventClient = new AddInjuryEventEndpointClient(baseUrl);
    await eventClient.addEvent(wrapper);

    newEventNotes = "";
    newEventPain = 0;
    newDate = new Date().toISOString().slice(0, 10);
    selectedSession = null;
    showEventModal = false;

    await loadInjuries();
  }

  function viewInjuryDetail(injury: InjuryDTO) {
    selectedInjury = injury;
  }

  function backToList() {
    selectedInjury = null;
  }

  async function resolveInjury(injuryId: string | null | undefined) {
    if (!injuryId) return;
    const client = new MarkInjuryResolvedEndpointClient(baseUrl);
    await client.resolve(injuryId);
    await loadInjuries();
    selectedInjury = null;
  }

  function openSessionPicker(context: "injury" | "event") {
    pickingFor = context;
    showSessionPicker = true;
  }

  function chooseSession(session: TrainingSessionDTO) {
    selectedSession = session;
    showSessionPicker = false;
  }

  onMount(async () => {
    await loadTrainingSessions();
    await loadInjuries();
  });
</script>


<div class="flex h-screen">
  <!-- Sidebar -->
  <div class="w-1/3 bg-base-200 p-4 overflow-y-auto flex flex-col">
    <div class="flex justify-between items-center mb-4">
      {#if selectedInjury}
        <button class="btn btn-outline btn-sm" on:click={backToList}>⬅ Back</button>
        <button class="btn btn-warning btn-sm" on:click={() => resolveInjury(selectedInjury?.injuryID)}>Resolve</button>
      {/if}
    </div>

    {#if selectedInjury}
      <div class="card bg-base-300 p-4 mb-2 shadow">
        <h3 class="font-semibold">{selectedInjury.category}</h3>
        <p class="text-sm">Initial: {selectedInjury.injuryDate}</p>
        <p class="text-sm">Status: {selectedInjury.isResolved ? "Resolved" : "Active"}</p>
      </div>

      {#each selectedInjury.injuryEvents.sort((a, b) => new Date(b.creationTime).getTime() - new Date(a.creationTime).getTime()) as evt}
        <div class="card bg-base-100 p-4 mb-2">
          <p class="text-sm">Session: {evt.trainingSessionID}</p>
          <p class="text-sm">Pain: {evt.painLevel}</p>
          <p class="text-sm">Type: {evt.injuryType}</p>
          <p class="text-sm">Notes: {evt.notes}</p>
          <p class="text-xs">Created: {evt.creationTime}</p>
        </div>
      {/each}
    {:else}
      {#each $injuries as injury (injury.injuryID)}
        <div class="card bg-base-100 p-4 mb-2 shadow cursor-pointer hover:bg-base-300" on:click={() => viewInjuryDetail(injury)}>
          <h3 class="font-semibold">{injury.category}</h3>
          <p class="text-sm">Initial: {injury.injuryDate}</p>
          <p class="text-sm">Last: {injury.injuryEvents.length ? injury.injuryEvents.slice().sort((a, b) => new Date(b.creationTime).getTime() - new Date(a.creationTime).getTime())[0].creationTime : 'N/A'}</p>
          <p class="text-sm">{injury.isResolved ? "Resolved" : "Active"}</p>
        </div>
      {/each}
    {/if}

    <button class="btn btn-accent mt-auto" on:click={() => selectedInjury ? showEventModal = true : showAddModal = true}>
      ＋ {selectedInjury ? "Add Event" : "Add Injury"}
    </button>
  </div>

  <!-- Main content -->
  <div class="w-2/3 p-6 space-y-4">
    <h1 class="text-3xl font-bold text-accent">Injury Tracker</h1>

    {#if selectedInjury}
      <div class="card bg-base-100 p-4">
        <h2 class="text-lg font-semibold">{selectedInjury.category}</h2>
        <p class="text-sm">Started: {selectedInjury.injuryDate}</p>
        <p class="text-sm">Resolved: {selectedInjury.isResolved ? "Yes" : "No"}</p>
      </div>
    {:else}
      <div class="card bg-base-100 p-6">
        <p class="text-sm italic text-center">Select an injury to view details</p>
      </div>
    {/if}
  </div>

  <!-- Add Injury Modal -->
  {#if showAddModal}
    <div class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div class="card bg-base-100 p-6 w-full max-w-md shadow-xl">
        <h2 class="text-xl font-bold mb-4">Add Injury + First Event</h2>
        <input class="input input-bordered mb-2" type="text" bind:value={newCategory} placeholder="Injury Category" />
        <input class="input input-bordered mb-2" type="date" bind:value={newDate} />
        <textarea class="textarea textarea-bordered mb-2" placeholder="Initial Event Notes" bind:value={newEventNotes}></textarea>
        <input class="input input-bordered mb-4" type="number" bind:value={newEventPain} placeholder="Pain Level (0-10)" />
        <button class="btn btn-outline w-full mb-2" on:click={() => openSessionPicker("injury")}>Attach Session</button>
        {#if selectedSession && pickingFor === "injury"}
        <p class="text-sm mb-2">Selected Session: #{selectedSession.sessionNumber} on {selectedSession.date}</p>
        {/if}
        <div class="flex gap-2">
          <button class="btn btn-accent flex-1" on:click={createInjuryWithFirstEvent}>Create</button>
          <button class="btn btn-outline flex-1" on:click={() => showAddModal = false}>Cancel</button>
        </div>
      </div>
    </div>
  {/if}

  <!-- Add Event Modal -->
  {#if showEventModal}
    <div class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div class="card bg-base-100 p-6 w-full max-w-md shadow-xl">
        <h2 class="text-xl font-bold mb-4">Add Injury Event</h2>
        <input class="input input-bordered mb-2" type="date" bind:value={newDate} />
        <textarea class="textarea textarea-bordered mb-2" placeholder="Event Notes" bind:value={newEventNotes}></textarea>
        <input class="input input-bordered mb-4" type="number" bind:value={newEventPain} placeholder="Pain Level (0-10)" />Pain Level (0-10)
        <button class="btn btn-outline w-full mb-2" on:click={() => openSessionPicker("event")}>Attach Session</button>
        {#if selectedSession && pickingFor === "event"}
        <p class="text-sm mb-2">Selected Session: #{selectedSession.sessionNumber} on {selectedSession.date}</p>
        {/if}
        <div class="flex gap-2">
          <button class="btn btn-info flex-1" on:click={addEventToInjury}>Add</button>
          <button class="btn btn-outline flex-1" on:click={() => showEventModal = false}>Cancel</button>
        </div>
      </div>
    </div>
  {/if}
  {#if showSessionPicker}
  <div class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
    <div class="card bg-base-100 p-6 w-full max-w-md shadow-xl">
      {#if !selectedProgram}
        <h2 class="text-lg font-bold mb-4">Select Program</h2>
        {#each programs as program}
          <div class="btn btn-block mb-2" on:click={() => selectedProgram = program}>{program.title}</div>
        {/each}
      {:else}
        <h2 class="text-lg font-bold mb-4">Select Session</h2>
        {#each selectedProgram.trainingSessions ?? [] as session}
          <div class="btn btn-outline btn-block mb-2" on:click={() => chooseSession(session)}>
            Session #{session.sessionNumber} – {session.date}
          </div>
        {/each}
        <button class="btn btn-outline mt-2 w-full" on:click={() => selectedProgram = null}>← Back to Programs</button>
      {/if}
    </div>
  </div>
{/if}

  


</div>