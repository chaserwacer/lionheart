<script lang="ts">
  import { createEventDispatcher } from 'svelte';
  import { addProgram } from '$lib/stores/programStore';
  import { v4 as uuid } from 'uuid';

  export let show: boolean;
  const dispatch = createEventDispatcher();

  let title = '';
  let startDate = '';
  let endDate = '';

  function close() {
    dispatch('close');
  }

  function createProgram() {
  if (!title || !startDate || !endDate) {
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
    tags: ['Powerlifting'],
    trainingSessions: []
  };

  addProgram(newProgram);

  // Reset form
  title = '';
  startDate = '';
  endDate = '';

  // Trigger parent update and close modal
  dispatch('created');
  dispatch('close');
}
</script>

{#if show}
  <div class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
    <div class="bg-white rounded-xl p-6 w-full max-w-md shadow-lg text-black">
      <div class="flex justify-between items-center mb-4">
        <h2 class="text-xl font-bold">Create New Program</h2>
        <button on:click={close} class="text-gray-600 hover:text-black text-lg font-bold">&times;</button>
      </div>

      <div class="space-y-4">
        <input bind:value={title} type="text" placeholder="Program Title" class="w-full px-3 py-2 border border-gray-300 rounded" />
        <input bind:value={startDate} type="date" class="w-full px-3 py-2 border border-gray-300 rounded" />
        <input bind:value={endDate} type="date" class="w-full px-3 py-2 border border-gray-300 rounded" />
      </div>

      <div class="flex justify-end mt-6 space-x-2">
        <button on:click={close} class="px-4 py-2 bg-gray-300 rounded hover:bg-gray-400">Cancel</button>
        <button on:click={createProgram} class="px-4 py-2 bg-green-500 text-white rounded hover:bg-green-600">Create</button>
      </div>
    </div>
  </div>
{/if}
