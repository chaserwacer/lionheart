<script lang="ts">
  import { createEventDispatcher, onMount } from 'svelte';
  import { browser } from '$app/environment';
  import {
    CreateTrainingProgramEndpointClient,
    CreateTrainingProgramRequest
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

  onMount(() => {
    if (browser) {
      client = new CreateTrainingProgramEndpointClient(baseUrl);
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
</script>


{#if show}
  <div class="fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center">
    <div class="bg-base-200 text-base-content rounded-lg w-full max-w-md border border-base-300 max-h-[90vh] flex flex-col">

      <!-- Scrollable form content -->
      <div class="p-6 overflow-y-auto" style="max-height: calc(90vh - 4rem);">
        <div class="flex justify-between items-center mb-4">
          <h2 class="text-2xl font-bold">Create New Program</h2>
          <button on:click={close} class="text-gray-400 hover:text-white text-2xl font-bold">&times;</button>
        </div>

        <div class="space-y-4">
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
        </div>
      </div>

      <!-- Sticky footer -->
      <div class="p-4 border-t border-base-300 bg-base-100 flex justify-end space-x-2">
        <button on:click={close} class="btn btn-ghost">Cancel</button>
        <button on:click={createProgram} class="btn btn-success">Create</button>
      </div>
    </div>
  </div>
{/if}
