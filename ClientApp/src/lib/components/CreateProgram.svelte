<script lang="ts">
  import { createEventDispatcher } from 'svelte';
  import { CreateTrainingProgramEndpointClient } from '$lib/api/ApiClient';
  import { CreateTrainingProgramRequest } from '$lib/api/ApiClient';

  export let show: boolean;
  const dispatch = createEventDispatcher();

  let title = '';
  let startDate = '';
  let endDate = '';
  let selectedTag = 'Powerlifting';

  const tagOptions = [
    'Powerlifting',
    'Bodybuilding',
    'General Fitness',
    'Running',
    'Biking',
    'Swimming'
  ];

  const client = new CreateTrainingProgramEndpointClient('/');

  function close() {
    dispatch('close');
  }

  async function createProgram() {
    if (!title || !startDate || !endDate || !selectedTag) {
      alert('All fields are required.');
      return;
    }
    const request = CreateTrainingProgramRequest.fromJS({
      title,
      startDate: new Date(startDate),
      endDate: new Date(endDate),
      tags: [selectedTag]
    });

    try {
      await client.create3(request);
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
    <div class="bg-zinc-900 rounded-lg p-6 w-full max-w-md text-white border border-zinc-700">
      <div class="flex justify-between items-center mb-4">
        <h2 class="text-2xl font-bold">Create New Program</h2>
        <button on:click={close} class="text-gray-400 hover:text-white text-2xl font-bold">&times;</button>
      </div>

      <div class="space-y-4">
        <input bind:value={title} type="text" placeholder="Program Title" class="w-full px-3 py-2 bg-zinc-800 border border-zinc-600 rounded text-white" />
        <input bind:value={startDate} type="date" class="w-full px-3 py-2 bg-zinc-800 border border-zinc-600 rounded text-white" />
        <input bind:value={endDate} type="date" class="w-full px-3 py-2 bg-zinc-800 border border-zinc-600 rounded text-white" />

        <select bind:value={selectedTag} class="w-full bg-zinc-800 border border-zinc-600 p-2 rounded text-white">
          {#each tagOptions as tag}
            <option value={tag}>{tag}</option>
          {/each}
        </select>
      </div>

      <div class="flex justify-end space-x-2 mt-6">
        <button on:click={close} class="px-4 py-2 bg-zinc-700 text-white rounded hover:bg-zinc-600">Cancel</button>
        <button on:click={createProgram} class="px-4 py-2 bg-green-600 text-white rounded hover:bg-green-500">Create</button>
      </div>
    </div>
  </div>
{/if}
