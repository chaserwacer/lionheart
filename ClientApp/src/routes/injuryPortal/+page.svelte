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
    TrainingSessionDTO,
    TrainingProgramDTO,
    TrainingSessionStatus,
    DeleteInjuryEndpointClient 
  } from "$lib/api/ApiClient";
  import { writable } from "svelte/store";

  const baseUrl = typeof window !== "undefined" ? window.location.origin : "http://localhost:5174";

  let injuries = writable<InjuryDTO[]>([]);
  let selectedInjury: InjuryDTO | null = null;
  let newCategory = "";
  let newDate = new Date().toISOString().slice(0, 10);
  let newEventNotes = "";
  let newEventPain = 0;
  let programs: TrainingProgramDTO[] = [];
  let selectedSession: TrainingSessionDTO | null = null;
  let selectedProgram: TrainingProgramDTO | null = null;

  let showAddModal = false;
  let showEventModal = false;
  let showSessionPicker = false;
  let pickingFor: "injury" | "event" = "injury";

  async function loadTrainingSessions() {
    const client = new GetTrainingProgramsEndpointClient(baseUrl);
    programs = await client.getAll4();
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
      const evtReq = new CreateInjuryEventRequest({
        trainingSessionID: selectedSession.trainingSessionID,
        notes: newEventNotes,
        painLevel: newEventPain,
        injuryType: InjuryEventType._0
      });
      const wrapper = new AddInjuryEventWrapper({ injuryId: injury.injuryID, request: evtReq });
      await new AddInjuryEventEndpointClient(baseUrl).addEvent(wrapper);
    }

    resetForm();
    await loadInjuries();
  }

  async function addEventToInjury() {
    if (!selectedInjury || !selectedSession) return;
    const evtReq = new CreateInjuryEventRequest({
      trainingSessionID: selectedSession.trainingSessionID,
      notes: newEventNotes,
      painLevel: newEventPain,
      injuryType: InjuryEventType._0
    });
    const wrapper = new AddInjuryEventWrapper({ injuryId: selectedInjury.injuryID, request: evtReq });
    await new AddInjuryEventEndpointClient(baseUrl).addEvent(wrapper);

    resetForm();
    showEventModal = false;
    await loadInjuries();
  }

  function resetForm() {
    newCategory = "";
    newEventNotes = "";
    newEventPain = 0;
    newDate = new Date().toISOString().slice(0, 10);
    selectedSession = null;
    showAddModal = false;
  }

  function viewInjuryDetail(injury: InjuryDTO) {
    selectedInjury = injury;
  }

  function backToList() {
    selectedInjury = null;
  }

  async function resolveInjury(injuryId?: string) {
    if (!injuryId) return;
    await new MarkInjuryResolvedEndpointClient(baseUrl).resolve(injuryId);
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
  async function deleteInjury(injuryId: string) {
    if (!confirm("Are you sure you want to delete this injury?")) return;
    await new DeleteInjuryEndpointClient(baseUrl).delete2(injuryId);
    await loadInjuries();
    selectedInjury = null;
  }
  let chatMessages: { role: "user" | "assistant"; content: string }[] = [];
  let chatInput = "";

  function sendMessage() {
    if (!chatInput.trim()) return;
    chatMessages = [...chatMessages, { role: "user", content: chatInput }];
    chatInput = "";
    // Fake response
    setTimeout(() => {
      chatMessages = [...chatMessages, { role: "assistant", content: "Thanks for your question! I’ll get back to you shortly." }];
    }, 500);
  }

  onMount(async () => {
    await loadTrainingSessions();
    await loadInjuries();
  });
</script>

<style>
  .sidebar {
    width: 50%;
    background: var(--pf-base-200);
    display: flex;
    flex-direction: column;
    height: 85vh;
    padding: 1rem;
    overflow: hidden; /* prevent entire sidebar scrolling */
  }
  .injury-list {
    flex: 1;
    overflow-y: auto;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    padding-right: 0.5rem; /* space for scrollbar */
  }
  .injury-card { cursor: pointer; }
  .injury-card:hover { background: var(--pf-base-300); }
  .add-button {
    margin-top: 1rem;
  }
  /* make add-button always visible */
  .sticky-button {
    position: sticky;
    bottom: 1rem;
    align-self: center;
    width: calc(100% - 2rem);
  }
  .chat-bubble {
    @apply bg-base-300 text-base-content rounded-xl p-3 max-w-xs;
  }
  .chat-start .chat-bubble {
    @apply bg-neutral text-neutral-content;
  }
  .chat-end .chat-bubble {
    @apply bg-accent text-accent-content;
  }

</style>

<div class="flex h-screen">
  <!-- Sidebar -->
  <aside class="sidebar">
    <div class="flex items-center justify-between mb-4">
      <h2 class="text-xl font-bold">{#if selectedInjury} Events {:else} Injuries {/if}</h2>
      {#if selectedInjury}
        <button class="btn btn-outline btn-sm" on:click={backToList}>Back</button>
        <button class="btn btn-warning btn-sm ml-2" on:click={() => resolveInjury(selectedInjury?.injuryID)}>Resolve</button>
      {/if}
    </div>

    <div class="injury-list flex flex-col">
      {#if selectedInjury}
        <!-- Selected injury header -->
        <div class="card bg-base-300 p-4 mb-2 shadow">
          <h3 class="font-semibold">{selectedInjury.category}</h3>
          <p class="text-sm">Started: {selectedInjury.injuryDate}</p>
          <p class="text-sm">Status: {selectedInjury.isResolved ? "Resolved" : "Active"}</p>
        </div>
        {#each selectedInjury.injuryEvents.sort((a, b) => new Date(b.creationTime).getTime() - new Date(a.creationTime).getTime()) as evt}
        <div class="card bg-base-100 p-3 mb-2">
          <p class="text-sm">Session: {evt.trainingSessionID}</p>
          <p class="text-sm">Pain: {evt.painLevel}</p>
          <p class="text-sm">{evt.injuryType}</p>
          <p class="text-xs italic">{evt.creationTime}</p>
          <p class="mt-1">{evt.notes}</p>
        </div>
        {/each}
      {:else}
        {#each $injuries as injury (injury.injuryID)}
          <div class="card bg-base-100 p-4 mb-2 shadow flex justify-between items-start">
            <button type="button" class="flex-1 injury-card text-left cursor-pointer bg-transparent border-none p-0 text-inherit" on:click={() => viewInjuryDetail(injury)}>
              <h3 class="font-semibold">{injury.category}</h3>
              <p class="text-sm">Initial: {injury.injuryDate}</p>
              <p class="text-sm">Last: {injury.injuryEvents.length ? injury.injuryEvents
                .slice()
                .sort((a, b) => new Date(b.creationTime).getTime() - new Date(a.creationTime).getTime())[0].creationTime : 'N/A'}</p>
              <p class="text-sm">{injury.isResolved ? "Resolved" : "Active"}</p>
            </button>
            <button
              class="btn btn-sm btn-outline btn-error ml-4 tooltip"
              data-tip="Delete Injury"
              on:click={() => deleteInjury(injury.injuryID)}>
             Delete
            </button>
          </div>
        {/each}
      {/if}
    </div>

    <button class="btn btn-accent add-button sticky-button" on:click={() => selectedInjury ? showEventModal = true : showAddModal = true}>
      ＋ {selectedInjury ? "Add Event" : "Add Injury"}
    </button>
  </aside>

  <!-- Main content -->
  <main class="w-2/3 p-6 space-y-4">
    <h1 class="text-3xl font-bold text-accent">Injury Tracker</h1>
    {#if !selectedInjury}
      <div class="card bg-base-100 p-6 text-center italic text-sm">Select an injury to view details</div>
    {/if}
  </main>

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

<!-- Chat Panel on the Right -->
<div class="w-full max-w-xl bg-base-100 border-l border-base-300 flex flex-col">
  <div class="p-4 border-b border-base-300 flex justify-between items-center">
    <h2 class="text-xl font-bold">AI Assistant</h2>
    <button class="btn btn-sm btn-outline" on:click={() => chatMessages = []}>New Chat</button>
  </div>

  <div class="flex-1 overflow-y-auto p-4 space-y-3" id="chatWindow">
    {#each chatMessages as msg}
      <div class="chat {msg.role === 'user' ? 'chat-end' : 'chat-start'}">
        <div class="chat-bubble">{msg.content}</div>
      </div>
    {/each}
  </div>

  <div class="p-4 border-t border-base-300">
    <textarea
      class="textarea textarea-bordered w-full h-24"
      bind:value={chatInput}
      placeholder="Ask something about injuries, training, recovery..."
    />
    <button class="btn btn-accent mt-2 w-full" on:click={sendMessage}>Send</button>
  </div>
</div>
</div>