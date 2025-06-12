<script lang="ts">
  import { createEventDispatcher } from 'svelte';
  import { addProgram } from '$lib/stores/programStore';
  import { v4 as uuid } from 'uuid';

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

  function close() {
    dispatch('close');
  }

  function createProgram() {
    if (!title || !startDate || !endDate || !selectedTag) {
      alert('All fields are required.');
      return;
    }

    const newProgram = {
      programID: uuid(),
      userID: 'temporary-user-id',
      title,
      startDate,
      endDate,
      nextTrainingSessionDate: startDate,
      tags: [selectedTag],
      trainingSessions: []
    };

    addProgram(newProgram);

    // Reset form
    title = '';
    startDate = '';
    endDate = '';
    selectedTag = 'Powerlifting';

    // Trigger parent update and close modal
    dispatch('created');
    dispatch('close');
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
