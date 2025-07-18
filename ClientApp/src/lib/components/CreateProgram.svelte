<script lang="ts">
  import { createEventDispatcher, onMount } from "svelte";
  import { browser } from "$app/environment";
  import {
    CreateTrainingProgramEndpointClient,
    CreateTrainingProgramRequest,
    GenerateProgramPreferencesEndpointClient,
    GenerateProgramFirstWeekEndpointClient,
    GenerateProgramRemainingWeeksEndpointClient,
    ProgramPreferencesDTO,
    FirstWeekGenerationDTO,
    RemainingWeeksGenerationDTO,
  } from "$lib/api/ApiClient";

  export let show: boolean;
  const dispatch = createEventDispatcher();

  let title = '';
  let startDate = '';
  let endDate = '';
  let selectedTag = 'Powerlifting';
  const tagOptions = ['Powerlifting', 'Bodybuilding', 'General Fitness', 'Running', 'Biking', 'Swimming'];

  const baseUrl = browser ? window.location.origin : "http://localhost:5174";
  let plainClient: CreateTrainingProgramEndpointClient | null = null;


  let prefClient: GenerateProgramPreferencesEndpointClient | null = null;
  let week1Client: GenerateProgramFirstWeekEndpointClient | null = null;
  let weekXClient: GenerateProgramRemainingWeeksEndpointClient | null = null;

  let aiStep = 0;
  let aiResponse: string | null = null;
  let isAiLoading = false;

  let daysPerWeek = "4";
  let preferredDays = "Mon, Wed, Fri";
  let squatDays = "2";
  let benchDays = "3";
  let deadliftDays = "1";
  let favoriteMovements = "";

  let trainingProgramID = '';

  onMount(() => {
    if (browser) {
      plainClient = new CreateTrainingProgramEndpointClient(baseUrl);
      prefClient = new GenerateProgramPreferencesEndpointClient(baseUrl);
      week1Client = new GenerateProgramFirstWeekEndpointClient(baseUrl);
      weekXClient = new GenerateProgramRemainingWeeksEndpointClient(baseUrl);
    }
  });

  function close() {
    dispatch('close');
  }
   function reset() {
    title = '';
    startDate = '';
    endDate = '';
    selectedTag = 'Powerlifting';
    aiStep = 0;
    aiResponse = null;
    trainingProgramID = '';
  }

  async function createProgram() {
    if (!title || !startDate || !endDate || !selectedTag) {
      alert("All fields are required.");
      return;
    }

    if (!plainClient) {
      alert("API client not initialized.");
      return;
    }

    const addDays = (date: string | Date, days: number): Date => {
      const d = new Date(date);
      d.setDate(d.getDate() + days);
      return d;
    };

    const request = CreateTrainingProgramRequest.fromJS({
      title,
      startDate: addDays(startDate, 1).toISOString().split("T")[0],
      endDate: addDays(endDate, 1).toISOString().split("T")[0],
      tags: [selectedTag],
    });

    try {
      await plainClient.create4(request);
      reset();
      dispatch("created");
    } catch (error) {
      console.error("Failed to create program:", error);
      alert("There was an error creating the program.");
    }
  }

async function createWithAi() {
  if (!plainClient) {
    alert('API client not initialized.');
    return;
  }
  if (!title || !startDate || !endDate || !selectedTag) {
    alert('All fields are required.');
    return;
  }

  aiStep = 1;
  isAiLoading = true;
  aiResponse = null;

  try {
    const request = CreateTrainingProgramRequest.fromJS({
      title: title.trim(),
      startDate: new Date(startDate).toISOString().split('T')[0],
      endDate: new Date(endDate).toISOString().split('T')[0],
      tags: [selectedTag]
    });
    const result = await plainClient.create4(request);
    trainingProgramID = result.trainingProgramID;
  } catch (error) {
    console.error('AI program creation error:', error);
    alert('There was an error creating the program.');
    aiStep = 0;
  } finally {
    isAiLoading = false;
  }
}

async function sendPreferences() {
  if (!prefClient || !trainingProgramID) return;

  isAiLoading = true;
  aiResponse = null;

  try {
    const prefDto = ProgramPreferencesDTO.fromJS({
      daysPerWeek,
      preferredDays,
      squatDays,
      benchDays,
      deadliftDays,
      favoriteMovements
    });
  await prefClient.preferences(prefDto);
    aiStep = 2; // move to week 1 generation button
  } catch (err) {
    console.error('Preferences error:', err);
    aiResponse = 'Error sending preferences.';
    aiStep = 1;
  } finally {
    isAiLoading = false;
  }
}

async function generateFirstWeek() {
  if (!week1Client || !trainingProgramID) return;

  isAiLoading = true;
  aiResponse = null;

  try {
    const firstDto = FirstWeekGenerationDTO.fromJS({ trainingProgramID });
    aiResponse = await week1Client.firstWeek(firstDto);
    aiStep = 3; // move to continue/next-week
  } catch (err) {
    console.error('AI week1 error:', err);
    aiResponse = 'Error generating first week.';
    aiStep = 2;
  } finally {
    isAiLoading = false;
  }
}

async function generateNextWeek() {
  if (!weekXClient) return;

  isAiLoading = true;
  aiResponse = null;

  try {
    const weekDto = RemainingWeeksGenerationDTO.fromJS({ trainingProgramID });
    aiResponse = await weekXClient.continueWeeks(weekDto);
    aiStep += 1;
  } catch (err) {
    console.error('AI weekX error:', err);
    aiResponse = 'Error generating additional weeks.';
    aiStep -= 1;
  } finally {
    isAiLoading = false;
  }
}

</script>

{#if show}
  <div
    class="fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center"
  >
    <div
      class="bg-base-200 text-base-content rounded-lg w-full max-w-md border border-base-300 max-h-[90vh] flex flex-col"
    >
      <!-- Scrollable form content -->
      <div
        class="p-6 overflow-y-auto space-y-4"
        style="max-height: calc(90vh - 6rem);"
      >
        <div class="flex justify-between items-center mb-2">
          <h2 class="text-2xl font-bold">Create New Program</h2>
          <button
            on:click={close}
            class="text-gray-400 hover:text-white text-2xl font-bold"
            >&times;</button
          >
        </div>

        <!-- Phase 0: Initial Inputs -->
        <input
          bind:value={title}
          type="text"
          placeholder="Program Title"
          class="input input-bordered w-full"
        />
        <input
          bind:value={startDate}
          type="date"
          class="input input-bordered w-full"
        />
        <input
          bind:value={endDate}
          type="date"
          class="input input-bordered w-full"
        />
        <select bind:value={selectedTag} class="select select-bordered w-full">
          {#each tagOptions as tag}
            <option value={tag}>{tag}</option>
          {/each}
        </select>


        <!-- Phase 3: Preferences Form -->
       {#if aiStep === 1}
      <div class="space-y-4 border-t border-base-300 pt-4">
        <h3 class="font-bold text-lg">User Preferences</h3>

        <div class="form-control">
          <label class="label">
            <span class="label-text">Days per week</span>
          </label>
          <input
            bind:value={daysPerWeek}
            type="number"
            min="1"
            max="7"
            placeholder="e.g. 4"
            class="input input-bordered w-full"
          />
        </div>

        <div class="form-control">
          <label class="label">
            <span class="label-text">Preferred days</span>
          </label>
          <input
            bind:value={preferredDays}
            type="text"
            placeholder="e.g. Mon, Wed, Fri"
            class="input input-bordered w-full"
          />
        </div>

        <div class="form-control">
          <label class="label">
            <span class="label-text">Squat days per week</span>
          </label>
          <input
            bind:value={squatDays}
            type="number"
            min="0"
            max="7"
            placeholder="e.g. 2"
            class="input input-bordered w-full"
          />
        </div>

        <div class="form-control">
          <label class="label">
            <span class="label-text">Bench days per week</span>
          </label>
          <input
            bind:value={benchDays}
            type="number"
            min="0"
            max="7"
            placeholder="e.g. 3"
            class="input input-bordered w-full"
          />
        </div>

        <div class="form-control">
          <label class="label">
            <span class="label-text">Deadlift days per week</span>
          </label>
          <input
            bind:value={deadliftDays}
            type="number"
            min="0"
            max="7"
            placeholder="e.g. 1"
            class="input input-bordered w-full"
          />
        </div>

        <div class="form-control">
          <label class="label">
            <span class="label-text">Favorite movements</span>
          </label>
          <input
            bind:value={favoriteMovements}
            type="text"
            placeholder="Comma-separated list"
            class="input input-bordered w-full"
          />
        </div>
      </div>
      {/if}


        {#if aiStep >= 2}
          <!-- Phase 2+: Week Confirmation & Continue -->
          <div class="space-y-2 border-t border-base-300 pt-4">
            <p class="text-sm text-gray-400">
              Review the generated week above, then click continue to add the
              next week.
            </p>
          </div>
        {/if}

        <!-- AI Response Display -->
        {#if isAiLoading}
          <div class="flex items-center space-x-2 mt-4">
            <span class="loading loading-spinner text-primary"></span>
            <span>Generating with AI...</span>
          </div>
        {:else if aiResponse}
          <div
            class="mt-4 bg-base-100 p-3 rounded border border-base-300 max-h-48 overflow-auto whitespace-pre-wrap text-sm"
          >
            {aiResponse}
          </div>
        {/if}
      </div>

      <!-- Sticky Footer -->
      <div
        class="p-4 border-t border-base-300 bg-base-100 flex justify-between"
      >
        <button on:click={close} class="btn btn-ghost">Cancel</button>
        <div class="flex space-x-2">
          {#if aiStep === 0}
            <button on:click={createProgram} class="btn btn-success">Create</button>
            <button on:click={createWithAi} class="btn btn-primary">Create with AI</button>

          {:else if aiStep === 1}
            <button on:click={sendPreferences} class="btn btn-primary">Submit Preferences</button>

          {:else if aiStep === 2}
            <button on:click={generateFirstWeek} class="btn btn-primary">Generate Week 1</button>

          {:else if aiStep >= 3}
            <button on:click={generateNextWeek} class="btn btn-primary">Generate Next Week</button>
          {/if}
        </div>
      </div>

    </div>
  </div>
{/if}


