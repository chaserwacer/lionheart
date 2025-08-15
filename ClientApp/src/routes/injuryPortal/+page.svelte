<script lang="ts">
  import { onMount } from "svelte";
  import { writable, get } from "svelte/store";
  import {
    GetTrainingProgramsEndpointClient,
    InjuryDTO,
    GetUserInjuriesEndpointClient,
    CreateInjuryEndpointClient,
    CreateInjuryEventEndpointClient,
    UpdateInjuryEventEndpointClient,
    UpdateInjuryEndpointClient,
    DeleteInjuryEndpointClient,
    DeleteInjuryEventEndpointClient,
    CreateInjuryRequest,
    CreateInjuryEventRequest,
    UpdateInjuryEventRequest,
    UpdateInjuryRequest,
    InjuryEventType,
    InjuryEventDTO,
  } from "$lib/api/ApiClient";
  import CompletedSessionViewer from "$lib/components/CompletedSessionViewer.svelte";
  import AddInjuryModal from "$lib/components/AddInjuryModal.svelte";
  import AddEventModal from "$lib/components/AddEventModal.svelte";
  import EventViewModal from "$lib/components/EventViewModal.svelte";
  import SessionPickerModal from "$lib/components/SessionPickerModal.svelte";

  const baseUrl =
    typeof window !== "undefined"
      ? window.location.origin
      : "http://localhost:5174";

  const injuries = writable<InjuryDTO[]>([]);
  let selectedInjury: InjuryDTO | null = null;
  let newName = "";
  // Local date in yyyy-MM-dd for inputs and DateOnly
  function localDateInput(d = new Date()) {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, "0");
    const day = String(d.getDate()).padStart(2, "0");
    return `${y}-${m}-${day}`;
  }
  let newDate = localDateInput();
  let newNotes = "";
  let newEventNotes = "";
  let newEventPain = 0;
  let newEventTypeStr: "checkin" | "treatment" = "checkin";
  let programs: any[] = [];
  let selectedSession: any | null = null;
  let selectedProgram: any | null = null;

  let showAddModal = false;
  let showEventModal = false;
  let showEventViewModal = false;
  let showSessionPicker = false;
  let pickingFor: "injury" | "event" = "injury";
  let expandedSessionForEvent: string | null = null; // trainingSessionID currently expanded
  let selectedEvent: {
    notes?: string;
    painLevel?: number;
    injuryType?: any;
    creationTime?: string;
    trainingSessionID?: string | number;
  } | null = null;

  function formatDate(d?: string | Date | null) {
    if (!d) return "";
    try {
      const dt = typeof d === "string" ? new Date(d) : d;
      if (Number.isNaN(dt.getTime())) return String(d ?? "");
      return dt.toLocaleDateString();
    } catch {
      return String(d ?? "");
    }
  }

  // For date-only values coming from the API (e.g., InjuryDate) which are serialized
  // as yyyy-MM-dd and may be parsed as UTC midnight, render using UTC parts to avoid
  // timezone shifts (e.g., EST/EDT showing the previous day).
  function formatDateOnly(d?: string | Date | null) {
    if (!d) return "";
    try {
      const dt = typeof d === "string" ? new Date(d) : d;
      if (Number.isNaN(dt.getTime())) return String(d ?? "");
      // Use UTC to display the intended calendar date without TZ drift
      return dt.toLocaleDateString(undefined, { timeZone: "UTC" });
    } catch {
      return String(d ?? "");
    }
  }

  function formatDateTime(d?: string | Date | null) {
    if (!d) return "";
    const dt = typeof d === "string" ? new Date(d) : d;
    if (Number.isNaN(dt.getTime())) return String(d ?? "");
    const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
    return dt.toLocaleString(undefined, {
      weekday: "short",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      timeZone: tz,
    });
  }

  function zeroPad(n: number, width = 2) {
    return String(n).padStart(width, "0");
  }

  function eventTypeLabel(t: InjuryEventType | undefined) {
    if (t === undefined || t === null) return "Event";
    // Per mapping elsewhere in this page: _1 = check-in, _0 = treatment
    return t === InjuryEventType._1 ? "Check-in" : "Treatment";
  }

  function painColorClass(pain?: number) {
    const p = pain ?? 0;
    if (p >= 7) return "text-error";
    if (p >= 4) return "text-warning";
    return "text-success";
  }

  // Average pain across an injury's events (0 if none). Rounded to 1 decimal for display.
  function avgPainLevel(injury?: InjuryDTO | null): number {
    const evts = injury?.injuryEvents ?? [];
    if (evts.length === 0) return 0;
    const sum = evts.reduce((acc, e) => acc + Number(e.painLevel ?? 0), 0);
    const avg = sum / evts.length;
    return Math.round(avg * 10) / 10;
  }

  function formatAvgPain(injury?: InjuryDTO | null): string {
    const avg = avgPainLevel(injury);
    return avg === 0 ? "0" : avg.toFixed(1);
  }

  // Clone helper to avoid mutating the store item by reference
  function clone<T>(obj: T): T {
    try {
      // structuredClone preserves Dates and nested objects
      // and is supported in modern browsers
      // @ts-ignore
      return structuredClone(obj);
    } catch {
      return JSON.parse(JSON.stringify(obj));
    }
  }

  // Reselect the canonical injury object from the store and decouple via clone
  function reselectCanonical() {
    if (!selectedInjury) return;
    const list = get(injuries);
    const match = list.find(
      (i) => String(i.injuryID) === String(selectedInjury?.injuryID),
    );
    if (match) {
      selectedInjury = clone(match);
    }
  }

  // Find a session from the prefetched programs list
  function findSessionById(sessionId?: string) {
    if (!sessionId) return null;
    for (const p of programs ?? []) {
      const session = p?.trainingSessions?.find(
        (s: any) => s.trainingSessionID === sessionId,
      );
      if (session) {
        return { session, slug: p.trainingProgramID };
      }
    }
    return null;
  }

  async function loadTrainingSessions() {
    const client = new GetTrainingProgramsEndpointClient(baseUrl);
    programs = await client.getAll5();
  }

  async function loadInjuries() {
    const client = new GetUserInjuriesEndpointClient(baseUrl);
    injuries.set(await client.getUserInjuries());
  }

  // Create-only flow for Add Injury
  async function createInjuryOnly() {
    const injuryClient = new CreateInjuryEndpointClient(baseUrl);
    // Interpret the yyyy-MM-dd input as a local date (midnight in local time)
    const [y, m, d] = newDate.split("-").map((x) => Number(x));
    const localDateOnly = new Date(y, (m ?? 1) - 1, d ?? 1);
    const request = new CreateInjuryRequest({
      name: newName,
      notes: newNotes,
      injuryDate: localDateOnly,
    });
    console.log("Creating injury:", request);
    await injuryClient.create2(request);
    resetForm();
    await loadInjuries();
  }

  async function addEventToInjury() {
    if (!selectedInjury) return;
    console.log("Adding event to injury:", selectedInjury);
    const evClient = new CreateInjuryEventEndpointClient(baseUrl);
    const mappedType =
      newEventTypeStr === "checkin" ? InjuryEventType._1 : InjuryEventType._0;
    var trainingSessionID = selectedSession?.trainingSessionID ?? null;
    const evtReq = new CreateInjuryEventRequest({
      injuryID: selectedInjury.injuryID,
      trainingSessionID: trainingSessionID,
      notes: newEventNotes,
      painLevel: newEventPain,
      injuryType: mappedType,
    });
    const saved = await evClient.addEvent(evtReq);
    // Update selection immediately with server result
    selectedInjury = saved;
    resetForm();
    showEventModal = false;
    await loadInjuries();
    // Re-select from the refreshed list so event timestamps/order match backend
    {
      const list = get(injuries);
      const match = list.find(
        (i) => String(i.injuryID) === String(saved?.injuryID),
      );
      if (match) selectedInjury = clone(match);
    }
  }

  function resetForm() {
    newName = "";
    newNotes = "";
    newEventNotes = "";
    newEventPain = 0;
    newEventTypeStr = "checkin";
  newDate = localDateInput();
    selectedSession = null;
    selectedProgram = null;
    showAddModal = false;
  }

  function viewInjuryDetail(injury: InjuryDTO) {
    console.log("Viewing injury details:", injury);
  // Decouple from the store item to avoid mutating list by reference
  selectedInjury = clone(injury);
  }

  function backToList() {
    selectedInjury = null;
  }

  // Update Injury (mirrors updateInjuryEvent pattern): mutate bound object, then persist
  async function updateInjury(injury: InjuryDTO) {
    if (!injury) return;
    const payload = new UpdateInjuryRequest({
      injuryID: injury.injuryID,
      name: injury.name,
      notes: injury.notes,
      isActive: injury.isActive,
    });
    const client = new UpdateInjuryEndpointClient(baseUrl);
    const saved = await client.update(payload);
    // Replace selection with server result, then refresh and reselect canonical copy
    selectedInjury = saved;
    await loadInjuries();
  reselectCanonical();
  }

  function persistSelectedInjury() {
    if (selectedInjury) {
      void updateInjury(selectedInjury);
    }
  }

  function openSessionPicker(context: "injury" | "event") {
    pickingFor = context;
    showSessionPicker = true;
  }

  function chooseSession(session: any) {
    selectedSession = session;
    showSessionPicker = false;
  }

  async function handleDeleteInjury(injuryId: string) {
    if (!confirm("Are you sure you want to delete this injury?")) return;
    await new DeleteInjuryEndpointClient(baseUrl).delete2(injuryId);
    await loadInjuries();
    selectedInjury = null;
  }

  // --- Injury Event Editing ---
  async function updateInjuryEvent(evt: InjuryEventDTO) {
    // Ensure enum is numeric
    const mappedType: InjuryEventType = Number(
      evt.injuryType,
    ) as InjuryEventType;
    const payload = new UpdateInjuryEventRequest({
      injuryEventID: String(evt.injuryEventID),
      painLevel: Number(evt.painLevel ?? 0),
      injuryType: mappedType,
      notes: String(evt.notes ?? ""),
    });
    const client = new UpdateInjuryEventEndpointClient(baseUrl);
    const saved = await client.eventPUT(payload);
    // Replace selected injury with server result to keep events fresh
    selectedInjury = saved;
    await loadInjuries();
    // Ensure we are showing the canonical injury from the store (correct sort/fields)
  reselectCanonical();
  }

  // --- Injury Event Deleting ---
  async function handleDeleteEvent(injuryEventId: string) {
    if (!injuryEventId) return;
    if (!confirm("Delete this event?")) return;
    const client = new DeleteInjuryEventEndpointClient(baseUrl);
    await client.eventDELETE(String(injuryEventId));
    await loadInjuries();
  reselectCanonical();
  }

  onMount(async () => {
    await loadTrainingSessions();
    await loadInjuries();
  });
</script>

<div class="flex h-full w-full bg-base-200">
  <!-- Sidebar -->
  <aside
    class="w-1/6 h-full bg-base-100 border-r border-base-300 flex flex-col"
  >
    <div
      class="px-4 py-3 border-b border-base-300 flex items-center justify-between w-full"
    >
      <h2 class="text-lg font-semibold">Injuries</h2>
      {#if selectedInjury}
        <div class="flex gap-2">
          <button class="btn btn-ghost btn-sm" on:click={backToList}
            >Back</button
          >
        </div>
      {/if}
    </div>

    <!-- Scrollable list -->
    <div class="flex-1 overflow-y-auto p-2 w-full">
      <ul class="menu menu-lg rounded-box w-full overflow-x-hidden">
        {#each $injuries as injury (injury.injuryID)}
          <li class="w-full border-b-2">
            <button
              type="button"
              class:active={selectedInjury?.injuryID === injury.injuryID}
              class="justify-between w-full"
              on:click={() => viewInjuryDetail(injury)}
            >
              <div class="flex flex-col w-full">
                <div class="flex font-semibold">{injury.name}</div>

                <div>
                  <span
                    class={`badge ${injury.isActive ? "badge-success" : "badge-ghost"}`}
                  >
                    {injury.isActive ? "Active" : "Inactive"}
                  </span>

                  <div class="flex flex-row items-center w-full">
                    <div class="flex-1 text-xs opacity-70">
                      {injury.injuryEvents.length
                        ? formatDate(
                            injury.injuryEvents
                              .slice()
                              .sort(
                                (a, b) =>
                                  new Date(b.creationTime).getTime() -
                                  new Date(a.creationTime).getTime(),
                              )[0].creationTime,
                          )
                        : formatDateOnly(injury.injuryDate)}
                    </div>
                  </div>
                </div>
              </div>
            </button>
          </li>
        {/each}
      </ul>
    </div>

    <!-- Sticky Add Injury button -->
    <div class="p-3 border-t border-base-300 bg-base-100">
      <button
        class="btn btn-accent w-full"
        on:click={() => (showAddModal = true)}
      >
        ＋ Add Injury
      </button>
    </div>
  </aside>

  <!-- Main content / Injury viewer -->
  <main class="flex-1 h-full p-6 overflow-y-auto">
    <div class="flex items-center justify-between mb-4">
      <h1 class="text-2xl font-bold">Injury Management System</h1>
      {#if selectedInjury}
        <div class="flex gap-2"></div>
      {/if}
    </div>

    {#if !selectedInjury}
      <div class="hero bg-base-100 border border-base-300 rounded-xl">
        <div class="hero-content text-center">
          <div class="max-w-md">
            <h2 class="text-xl font-semibold mb-2">Select an injury</h2>
            <p class="opacity-70">
              Pick an injury from the list to view details, or add a new one.
            </p>
          </div>
        </div>
      </div>
    {:else}
      <!-- Injury details header -->
      <div class="card bg-base-100 border border-base-300 shadow-sm mb-4">
        <div class="card-body gap-2">
          <div
            class="flex items-start justify-between gap-4 flex-col md:flex-row"
          >
            <div class="flex-1 space-y-2">
              <input
                class="input input-lg text-bold font-bold input-bordered w-full"
                type="text"
                bind:value={selectedInjury.name}
                on:change={persistSelectedInjury}
              />
              <textarea
                class="textarea textarea-bordered w-full"
                rows="3"
                bind:value={selectedInjury.notes}
                on:change={persistSelectedInjury}
              />
            </div>
          </div>
          <div
            class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 text-sm"
          >
            <div class="stat p-3 rounded-box bg-base-200">
              <div class="stat-title">Status</div>
              <div class="stat-value text-lg">
                <label class="label m-o p-o text-center cursor-pointer swap">
                  <input
                    type="checkbox"
                    class="toggle toggle-success w-24 h-10 text-center"
                    bind:checked={selectedInjury.isActive}
                    on:change={persistSelectedInjury}
                  />
                  <div class="swap-on">ACTIVE</div>
                  <div class="swap-off">INACTIVE</div>
                </label>
              </div>
            </div>
            <div class="stat p-3 rounded-box bg-base-200">
              <div class="stat-title">Avg Pain</div>
              <div
                class={`stat-value text-lg ${painColorClass(avgPainLevel(selectedInjury))}`}
              >
                {formatAvgPain(selectedInjury)}/10
              </div>
            </div>
      <div class="stat p-3 rounded-box bg-base-200">
              <div class="stat-title">Started On</div>
              <div class="stat-value text-lg">
        {formatDateOnly(selectedInjury.injuryDate)}
              </div>
            </div>
            <div class="stat p-3 rounded-box bg-base-200">
              <div class="stat-title">Last Event</div>
              <div class="stat-value text-lg">
                {#if (selectedInjury.injuryEvents?.length ?? 0) > 0}
                  {formatDate(
                    selectedInjury.injuryEvents
                      .slice()
                      .sort(
                        (a, b) =>
                          new Date(b.creationTime).getTime() -
                          new Date(a.creationTime).getTime(),
                      )[0].creationTime,
                  )}
                {/if}
              </div>
            </div>
          </div>
          <div class="flex w-full gap-2">
            <button
              class="btn btn-primary btn-sm"
              on:click={() => (showEventModal = true)}>Track New Event</button
            >
            <button
              class="btn btn-error btn-sm"
              on:click={() =>
                selectedInjury && handleDeleteInjury(selectedInjury.injuryID)}
            >
              Delete Injury
            </button>
          </div>
        </div>
      </div>

      <!-- Events list -->
      <section>
        <div class="flex items-center justify-between mb-2">
          <h3 class="text-lg font-semibold">Injury Events</h3>
        </div>
        {#if (selectedInjury.injuryEvents?.length ?? 0) === 0}
          <div class="alert">
            <span>No events yet. Track the first event.</span>
          </div>
        {:else}
          <ul
            class="list bg-base-100 rounded-box shadow-md border border-base-300"
          >
            {#each selectedInjury.injuryEvents
              .slice()
              .sort((a, b) => new Date(b.creationTime).getTime() - new Date(a.creationTime).getTime()) as evt, i}
              <li
                class="list-row grid grid-cols-[auto_auto_1fr_auto_auto] items-start gap-3 border p-4"
              >
                <!-- Left index number -->
                <div class="text-4xl font-thin opacity-30 tabular-nums">
                  {zeroPad((selectedInjury.injuryEvents?.length ?? 0) - i)}
                </div>

                <!-- Edit-friendly pain control -->
                <div class="flex flex-col items-center gap-1 w-16">
                  <span class="text-xs opacity-70">Pain</span>
                  <button
                    class="btn btn-ghost btn-xs leading-none"
                    title="Increase"
                    on:click={() => {
                      evt.painLevel = Math.min(
                        10,
                        Number(evt.painLevel ?? 0) + 1,
                      );
                      updateInjuryEvent(evt);
                    }}>▲</button
                  >
                  <div
                    class={`text-sm font-bold ${painColorClass(evt.painLevel)}`}
                  >
                    {evt.painLevel ?? 0}/10
                  </div>
                  <button
                    class="btn btn-ghost btn-xs leading-none"
                    title="Decrease"
                    on:click={() => {
                      evt.painLevel = Math.max(
                        0,
                        Number(evt.painLevel ?? 0) - 1,
                      );
                      updateInjuryEvent(evt);
                    }}>▼</button
                  >
                </div>

                <!-- Main content -->
                <div class="list-col-grow min-w-0">
                  <div class="flex flex-wrap items-center gap-2">
                    <!-- Editable type selector -->
                    <select
                      class="select select-bordered select-xs"
                      bind:value={evt.injuryType}
                      on:change={() => updateInjuryEvent(evt)}
                    >
                      <option value={InjuryEventType._1}>Check-in</option>
                      <option value={InjuryEventType._0}>Treatment</option>
                    </select>
                  </div>
                  <div class="text-xs opacity-60 mt-0.5">
                    {formatDateTime(evt.creationTime)}
                  </div>
                  <div class="text-xs opacity-70">
                    {#if evt.trainingSessionID}
                      {#if findSessionById(String(evt.trainingSessionID))}
                        <a
                          class="link link-primary"
                          href={`/programs/${findSessionById(String(evt.trainingSessionID))?.slug}/session/${evt.trainingSessionID}`}
                          >View session page →</a
                        >
                      {/if}
                    {:else}
                      <span class="opacity-50">No session attached</span>
                    {/if}
                  </div>
                  <div class="mt-2">
                    <label class="label py-1" for="event-notes-{i}"
                      ><span class="label-text text-xs font-bold">Notes</span
                      ></label
                    >
                    <textarea
                      id="event-notes-{i}"
                      class="textarea textarea-bordered textarea-xs w-full"
                      rows="3"
                      placeholder="Add notes"
                      bind:value={evt.notes}
                      on:change={(e) => updateInjuryEvent(evt)}
                    ></textarea>
                  </div>
                </div>

                <!-- Actions: toggle inline session viewer if available -->
                {#if evt.trainingSessionID}
                  <button
                    class="btn btn-square btn-ghost"
                    title="Toggle session details"
                    on:click={() => {
                      const id = String(evt.trainingSessionID);
                      expandedSessionForEvent =
                        expandedSessionForEvent === id ? null : id;
                    }}
                  >
                    <svg
                      class="size-[1.2em]"
                      xmlns="http://www.w3.org/2000/svg"
                      viewBox="0 0 24 24"
                      ><g
                        stroke-linejoin="round"
                        stroke-linecap="round"
                        stroke-width="2"
                        fill="none"
                        stroke="currentColor"
                        ><path d="M6 3L20 12 6 21 6 3z"></path></g
                      ></svg
                    >
                  </button>
                {/if}

                <!-- Delete event button -->
                <button
                  class="btn btn-square btn-ghost text-error"
                  title="Delete event"
                  on:click={() => handleDeleteEvent(String(evt.injuryEventID))}
                  >✕</button
                >

                <!-- Inline dropdown session viewer -->
                {#if evt.trainingSessionID && expandedSessionForEvent === String(evt.trainingSessionID)}
                  {@const sessionData = findSessionById(
                    String(evt.trainingSessionID),
                  )}
                  {#if sessionData}
                    <div class="list-col-wrap col-span-5 w-full mt-3">
                      <div class="collapse collapse-open bg-base-200/50">
                        <div class="collapse-content p-2">
                          <!-- Small screens: completed session viewer -->
                          <div class="md:hidden">
                            <CompletedSessionViewer
                              session={sessionData.session}
                              slug={sessionData.slug}
                              loadSessions={loadTrainingSessions}
                            />
                          </div>
                          <!-- Medium and up: concise DTO table view -->
                          <div class="hidden md:block text-sm">
                            <div class="mb-2">
                              <div class="font-semibold">
                                Session #{sessionData.session.sessionNumber}
                              </div>
                              {#if sessionData.session.notes}
                                <div
                                  class="mt-1 p-2 rounded bg-base-200 max-h-24 overflow-y-auto"
                                >
                                  {sessionData.session.notes}
                                </div>
                              {/if}
                            </div>
                            {#if sessionData.session.movements && sessionData.session.movements.length > 0}
                              {#each sessionData.session.movements as movement}
                                <div class="mb-4">
                                  <div class="flex flex-row gap-2 items-center">
                                    <h4 class="font-semibold">
                                      {movement.movementModifier.name}
                                      {movement.movementBase.name}
                                    </h4>
                                    <div class="badge badge-xs p-2">
                                      {movement.movementModifier.equipment
                                        ?.name}
                                    </div>
                                  </div>
                                  <table
                                    class="table table-xs w-full border rounded-lg overflow-hidden"
                                  >
                                    <thead class="bg-base-300">
                                      <tr>
                                        <th class="px-2 py-1">Set</th>
                                        <th class="px-2 py-1">Weight</th>
                                        <th class="px-2 py-1">Reps</th>
                                        <th class="px-2 py-1">RPE</th>
                                      </tr>
                                    </thead>
                                    <tbody>
                                      {#each movement.sets as set, si}
                                        <tr class="border-t">
                                          <td class="px-2 py-1">{si + 1}</td>
                                          <td class="px-2 py-1"
                                            >{set.actualWeight}</td
                                          >
                                          <td class="px-2 py-1"
                                            >{set.actualReps}</td
                                          >
                                          <td class="px-2 py-1"
                                            >{set.actualRPE}</td
                                          >
                                        </tr>
                                      {/each}
                                    </tbody>
                                  </table>
                                </div>
                              {/each}
                            {:else}
                              <p class="text-xs opacity-70">
                                No movements found.
                              </p>
                            {/if}
                          </div>
                        </div>
                      </div>
                    </div>
                  {:else}
                    <div
                      class="list-col-wrap col-span-5 w-full mt-3 text-xs opacity-60"
                    >
                      Session details unavailable.
                    </div>
                  {/if}
                {/if}
              </li>
            {/each}
          </ul>
        {/if}
      </section>
    {/if}
  </main>
</div>

<!-- Add Injury Modal (create injury only) -->
{#if showAddModal}
  <AddInjuryModal
    bind:newName
    bind:newDate
    bind:newNotes
    onCreate={createInjuryOnly}
    onCancel={() => (showAddModal = false)}
  />
{/if}

<!-- Add Event Modal -->
{#if showEventModal}
  <AddEventModal
    bind:newEventNotes
    bind:newEventPain
    bind:newEventTypeStr
    {selectedSession}
    onOpenSessionPicker={() => openSessionPicker("event")}
    onAdd={addEventToInjury}
    onCancel={() => (showEventModal = false)}
  />
{/if}

<!-- Event View Modal -->
{#if showEventViewModal && selectedEvent}
  <EventViewModal
    {selectedEvent}
    onClose={() => {
      showEventViewModal = false;
      selectedEvent = null;
    }}
  />
{/if}

<!-- Session Picker -->
{#if showSessionPicker}
  <SessionPickerModal
    {programs}
    bind:selectedProgram
    bind:selectedSession
    onClose={() => (showSessionPicker = false)}
  />
{/if}
