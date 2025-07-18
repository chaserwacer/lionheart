<script lang="ts">
  import { createEventDispatcher, onMount } from "svelte";
  import { browser } from "$app/environment";
  import {
    CreateTrainingProgramEndpointClient,
    CreateTrainingProgramRequest,
    GenerateProgramInitializationEndpointClient,
    GenerateProgramShellEndpointClient,
    GenerateProgramPreferencesEndpointClient,
    GenerateProgramFirstWeekEndpointClient,
    GenerateProgramRemainingWeeksEndpointClient,
    ProgramShellDTO,
    ProgramPreferencesDTO,
    FirstWeekGenerationDTO,
    RemainingWeeksGenerationDTO,
  } from "$lib/api/ApiClient";

  export let show: boolean;
  const dispatch = createEventDispatcher();

  let title = "";
  let startDate = "";
  let endDate = "";
  let selectedTag = "Powerlifting";

  const tagOptions = [
    "Powerlifting",
    "Bodybuilding",
    "General Fitness",
    "Running",
    "Biking",
    "Swimming",
  ];

  const baseUrl = browser ? window.location.origin : "http://localhost:5174";
  let plainClient: CreateTrainingProgramEndpointClient | null = null;

  // AI clients
  let initClient: GenerateProgramInitializationEndpointClient | null = null;
  let shellClient: GenerateProgramShellEndpointClient | null = null;
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

  let trainingProgramID: string = "";

  onMount(() => {
    if (browser) {
      plainClient = new CreateTrainingProgramEndpointClient(baseUrl);
      initClient = new GenerateProgramInitializationEndpointClient(baseUrl);
      shellClient = new GenerateProgramShellEndpointClient(baseUrl);
      prefClient = new GenerateProgramPreferencesEndpointClient(baseUrl);
      week1Client = new GenerateProgramFirstWeekEndpointClient(baseUrl);
      weekXClient = new GenerateProgramRemainingWeeksEndpointClient(baseUrl);
    }
  });

  function close() {
    dispatch("close");
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
    if (!initClient || !shellClient) return;

    aiStep = 1;
    isAiLoading = true;
    aiResponse = null;

    try {
      aiResponse = await initClient.init();
      console.log("Initialization AI Response:", aiResponse);

      aiStep = 2;

      const shellDto = ProgramShellDTO.fromJS({
        title: title.trim(),
        startDate: new Date(startDate).toISOString().split("T")[0], // ✅ Format as string
        endDate: new Date(endDate).toISOString().split("T")[0], // ✅ Format as string
        tag: selectedTag,
      });

      console.log("Sending ProgramShellDTO:", shellDto);
      console.log("Sending ProgramShellDTO (JSON):", shellDto.toJSON());
      aiResponse = await shellClient.shell(shellDto);
      console.log("AI Shell Response:", aiResponse);
    } catch (err) {
      console.error("AI shell error:", err);

      if (err instanceof Response) {
        const errorText = await err.text();
        console.error("API Error Body:", errorText);
      }

      aiResponse = "Error during AI program creation.";
    } finally {
      isAiLoading = false;
    }
  }

  async function sendPreferences() {
    if (!prefClient) return;

    aiStep = 3;
    isAiLoading = true;
    aiResponse = null;

    try {
      const prefDto = ProgramPreferencesDTO.fromJS({
        daysPerWeek,
        preferredDays,
        squatDays,
        benchDays,
        deadliftDays,
        favoriteMovements,
      });

      aiResponse = await prefClient.preferences(prefDto);
      aiStep = 4;

      const firstDto = FirstWeekGenerationDTO.fromJS({
        trainingProgramID,
      });

      aiResponse = await week1Client!.weekOne(firstDto);
    } catch (err) {
      console.error("AI preference/week1 error:", err);
      aiResponse = "Error during preference processing.";
    } finally {
      isAiLoading = false;
    }
  }

  async function generateNextWeek() {
    if (!weekXClient) return;

    aiStep += 1;
    isAiLoading = true;
    aiResponse = null;

    try {
      const weekDto = RemainingWeeksGenerationDTO.fromJS({});
      aiResponse = await weekXClient.continueWeeks(weekDto);
    } catch (err) {
      console.error("AI weekX error:", err);
      aiResponse = "Error generating additional weeks.";
    } finally {
      isAiLoading = false;
    }
  }

  function reset() {
    title = "";
    startDate = "";
    endDate = "";
    selectedTag = "Powerlifting";
    aiStep = 0;
    aiResponse = null;
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

        {#if aiStep < 3}
          <p class="text-sm text-gray-400 italic">
            Or generate with AI and input your training preferences in the next
            step.
          </p>
        {/if}

        <!-- Phase 3: Preferences Form -->
        {#if aiStep === 3}
          <div class="space-y-2 border-t border-base-300 pt-4">
            <h3 class="font-bold">User Preferences</h3>
            <input
              bind:value={daysPerWeek}
              type="number"
              min="1"
              max="7"
              placeholder="Days per week"
              class="input input-bordered w-full"
            />
            <input
              bind:value={preferredDays}
              type="text"
              placeholder="Preferred training days (e.g. Mon, Wed, Fri)"
              class="input input-bordered w-full"
            />
            <input
              bind:value={squatDays}
              type="number"
              min="0"
              max="7"
              placeholder="Squat days per week"
              class="input input-bordered w-full"
            />
            <input
              bind:value={benchDays}
              type="number"
              min="0"
              max="7"
              placeholder="Bench days per week"
              class="input input-bordered w-full"
            />
            <input
              bind:value={deadliftDays}
              type="number"
              min="0"
              max="7"
              placeholder="Deadlift days per week"
              class="input input-bordered w-full"
            />
            <input
              bind:value={favoriteMovements}
              type="text"
              placeholder="Favorite movements (comma-separated)"
              class="input input-bordered w-full"
            />
          </div>
        {/if}

        <!-- Phase 4+: Week Confirmation & Continue -->
        {#if aiStep >= 4}
          <div class="space-y-2 border-t border-base-300 pt-4">
            <p class="text-sm text-gray-400">
              Review the generated week above, then click continue to add the
              next week.
            </p>
            <button on:click={generateNextWeek} class="btn btn-accent w-full"
              >Generate Next Week</button
            >
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
            <button on:click={createProgram} class="btn btn-success"
              >Create</button
            >
            <button on:click={createWithAi} class="btn btn-primary"
              >Create with AI</button
            >
          {:else if aiStep === 3}
            <button on:click={sendPreferences} class="btn btn-primary"
              >Continue</button
            >
          {:else if aiStep >= 4}
            <button on:click={generateNextWeek} class="btn btn-primary"
              >Continue</button
            >
          {/if}
        </div>
      </div>
    </div>
  </div>
{/if}
