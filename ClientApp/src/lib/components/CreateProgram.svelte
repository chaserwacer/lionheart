<script lang="ts">
  import { createEventDispatcher, onMount } from 'svelte';
  import { browser } from '$app/environment';
  import {
    CreateTrainingProgramEndpointClient,
    CreateTrainingProgramRequest,
    GenerateOpenAIPromptEndpointClient,
    GeneratePromptRequest
  } from '$lib/api/ApiClient';

  export let show: boolean;
  const dispatch = createEventDispatcher();

  let title = '';
  let startDate = '';
  let endDate = '';
  let selectedTag = 'Powerlifting';

  const baseUrl = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5174';

  const tagOptions = [
    'Powerlifting',
    'Bodybuilding',
    'General Fitness',
    'Running',
    'Biking',
    'Swimming'
  ];

  let client: CreateTrainingProgramEndpointClient | null = null;
  let aiClient: GenerateOpenAIPromptEndpointClient | null = null;

  let aiStep = 0;
  let aiResponse: string | null = null;
  let isAiLoading = false;

  onMount(() => {
    if (browser) {
      client = new CreateTrainingProgramEndpointClient(baseUrl);
      aiClient = new GenerateOpenAIPromptEndpointClient(baseUrl);
    }
  });

  function close() {
    dispatch('close');
  }

  async function createProgram() {
    if (!title || !startDate || !endDate || !selectedTag) {
      alert('All fields are required.');
      return;
    }

    if (!client) {
      alert('API client not initialized.');
      return;
    }

    const request = CreateTrainingProgramRequest.fromJS({
      title,
      startDate: new Date(startDate).toISOString().split('T')[0],
      endDate: new Date(endDate).toISOString().split('T')[0],
      tags: [selectedTag]
    });

    try {
      await client.create4(request);

      // Reset form
      title = '';
      startDate = '';
      endDate = '';
      selectedTag = 'Powerlifting';

      dispatch('created');
      dispatch('close');
    } catch (error) {
      console.error('Failed to create program:', error);
      alert('There was an error creating the program.');
    }
  }

  async function createWithAi() {
    if (!title || !startDate || !endDate) {
      alert('Title and dates are required.');
      return;
    }

    if (!aiClient) {
      alert('AI client not initialized.');
      return;
    }

    aiStep = 1;
    isAiLoading = true;
    aiResponse = null;

    const req: GeneratePromptRequest = GeneratePromptRequest.fromJS({
      promptType: 'genprog.04.step1',
      inputs: {
        title,
        startDate,
        endDate,
         lengthWeeks: '3', // Add this back
        tag: selectedTag
      }
    });

    try {
      aiResponse = await aiClient.prompt(req);
    } catch (err) {
      console.error('AI generation error:', err);
      aiResponse = 'Error during AI generation.';
    } finally {
      isAiLoading = false;
    }
  }
</script>



{#if show}
  <div class="fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center">
    <div class="bg-base-200 text-base-content rounded-lg w-full max-w-md border border-base-300 max-h-[90vh] flex flex-col">

      <!-- Scrollable form content -->
      <div class="p-6 overflow-y-auto space-y-4" style="max-height: calc(90vh - 6rem);">
        <div class="flex justify-between items-center mb-2">
          <h2 class="text-2xl font-bold">Create New Program</h2>
          <button on:click={close} class="text-gray-400 hover:text-white text-2xl font-bold">&times;</button>
        </div>

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

        {#if isAiLoading}
          <div class="flex items-center space-x-2 mt-4">
            <span class="loading loading-spinner text-primary"></span>
            <span>Generating with AI...</span>
          </div>
        {:else if aiResponse}
          <div class="mt-4 bg-base-100 p-3 rounded border border-base-300 max-h-48 overflow-auto whitespace-pre-wrap text-sm">
            {aiResponse}
          </div>
        {/if}
      </div>

      <!-- Sticky footer -->
      <div class="p-4 border-t border-base-300 bg-base-100 flex justify-between">
        <button on:click={close} class="btn btn-ghost">Cancel</button>
        <div class="flex space-x-2">
          <button on:click={createProgram} class="btn btn-success">Create</button>
          <button on:click={createWithAi} class="btn btn-primary">Create with AI</button>
        </div>
      </div>
    </div>
  </div>
{/if}
