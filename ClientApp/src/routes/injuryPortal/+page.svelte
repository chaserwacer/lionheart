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
    InjuryEventType
  } from "$lib/api/ApiClient";
  import { writable } from "svelte/store";

  const baseUrl =
    typeof window !== "undefined" ? window.location.origin : "http://localhost:5174";

  let injuries = writable<InjuryDTO[]>([]);
  let newCategory = "";
  let newDate = new Date().toISOString().slice(0, 10);
  let newEventNotes = "";
  let newEventPain = 0;
  let selectedInjuryId: string | null = null;
  let trainingSessionID = "";

  async function loadInjuries() {
    const client = new GetUserInjuriesEndpointClient(baseUrl);
    injuries.set(await client.getUserInjuries());
  }

  async function createInjury() {
    const client = new CreateInjuryEndpointClient(baseUrl);
    const request = new CreateInjuryRequest({
      category: newCategory,
      injuryDate: new Date(newDate) // Convert string to Date object
    });
    await client.create2(request);
    await loadInjuries();
    newCategory = "";
  }

  async function resolveInjury(injuryId: string) {
    const client = new MarkInjuryResolvedEndpointClient(baseUrl);
    await client.resolve(injuryId);
    await loadInjuries();
  }

  async function addInjuryEvent() {
    if (!selectedInjuryId) return;

    const client = new AddInjuryEventEndpointClient(baseUrl);

    const eventRequest = new CreateInjuryEventRequest({
      trainingSessionID,
      notes: newEventNotes,
      injuryType: InjuryEventType._0
    });

    const wrapper = new AddInjuryEventWrapper({
      injuryId: selectedInjuryId,
      request: eventRequest
    });

    await client.addEvent(wrapper); // ✅ method updated to match OpenAPI codegen
    await loadInjuries();

    newEventNotes = "";
    trainingSessionID = "";
  }

  onMount(loadInjuries);
</script>


<div class="p-5">
  <h1 class="text-3xl font-bold mb-4 text-accent">Injury Tracker</h1>

  <div class="card bg-base-100 shadow-xl p-5 mb-6">
    <h2 class="text-xl font-semibold">Add Injury</h2>
    <div class="flex flex-col gap-2 mt-2">
      <input class="input input-bordered" type="text" bind:value={newCategory} placeholder="Injury Category" />
      <input class="input input-bordered" type="date" bind:value={newDate} />
      <button class="btn btn-accent" on:click={createInjury}>Create Injury</button>
    </div>
  </div>

  <div class="divider">Current Injuries</div>
  {#each $injuries as injury (injury.injuryID)}
    <div class="card bg-base-200 p-4 mb-4">
      <h2 class="font-bold text-lg">{injury.category}</h2>
      <p class="text-sm">Date: {injury.injuryDate}</p>
      <p class="text-sm">Resolved: {injury.isResolved ? "Yes" : "No"}</p>

      {#if !injury.isResolved}
        <button class="btn btn-success btn-sm my-2" on:click={() => resolveInjury(injury.injuryID)}>Mark Resolved</button>

        <div class="mt-2">
          <h3 class="text-md font-semibold">Add Event</h3>
          <input class="input input-bordered input-sm my-1" placeholder="Session ID" bind:value={trainingSessionID} />
          <textarea class="textarea textarea-bordered my-1" bind:value={newEventNotes} placeholder="Notes"></textarea>
          <input class="input input-bordered input-sm my-1" type="number" bind:value={newEventPain} placeholder="Pain Level (0-10)" />
          <button class="btn btn-info btn-sm" on:click={() => { selectedInjuryId = injury.injuryID; addInjuryEvent(); }}>Add Event</button>
        </div>
      {/if}

      {#if injury.injuryEvents.length > 0}
        <div class="mt-2">
          <h3 class="text-md font-semibold">Events</h3>
          <ul class="text-sm">
            {#each injury.injuryEvents as evt}
              <li class="border-t py-1">
                [Session {evt.trainingSessionID}] Pain: {evt.painLevel} ({evt.injuryType}) — {evt.notes}
              </li>
            {/each}
          </ul>
        </div>
      {/if}
    </div>
  {/each}
</div>
