<script lang="ts">
  import { createEventDispatcher, onMount } from 'svelte';
  import { browser } from '$app/environment';
  import {
    UpdateTrainingProgramEndpointClient,
    UpdateTrainingProgramRequest,
    TrainingProgramDTO
  } from '$lib/api/ApiClient';

  export let show: boolean;
  export let program: TrainingProgramDTO;
  const dispatch = createEventDispatcher();

  let title = '';
  let startDate = '';
  let endDate = '';
  let selectedTags: string[] = [];
  let isCompleted = false;
  let selectedTag: string = '';

  const tagOptions = ['Powerlifting', 'Bodybuilding', 'General Fitness', 'Running', 'Biking', 'Swimming'];
  const baseUrl = browser ? window.location.origin : 'http://localhost:5174';
  let client: UpdateTrainingProgramEndpointClient | null = null;

  onMount(() => {
    if (browser) {
      client = new UpdateTrainingProgramEndpointClient(baseUrl);
    }
    if (program) {
      title = program.title;
      startDate = program.startDate.toString();
      endDate = program.endDate.toString();
      selectedTags = [...program.tags];
      isCompleted = program.isCompleted;
      selectedTag = program ? program.tags[0] ?? '' : '';
    }
  });

  function close() {
    dispatch('close');
  }

  const addDays = (date: string | Date, days: number): Date => {
      const d = new Date(date);
      d.setDate(d.getDate() + days);
      return d;
    };

  async function saveEdits() {
    if (!title || !startDate || !endDate || !selectedTag) {
      alert('All fields are required, including a tag.');
      return;
    }
    if (!client) {
      alert('API client not initialized.');
      return;
    }
   
    const request = UpdateTrainingProgramRequest.fromJS({
      trainingProgramID: program.trainingProgramID,
      title,
      startDate : addDays(startDate, 1).toISOString().split('T')[0],
      endDate : addDays(endDate, 1).toISOString().split('T')[0],
      tags: [selectedTag],
      isCompleted
    });
    try {
      await client.update3(request);
      dispatch('saved');
      close();
    } catch (error) {
      console.error('Failed to update program:', error);
      alert('There was an error updating the program.');
    }
  }
</script>

{#if show}
  <div class="fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center">
    <div class="bg-base-200 text-base-content rounded-lg w-full max-w-md border border-base-300 max-h-[90vh] flex flex-col">
      <div class="p-6 overflow-y-auto space-y-4" style="max-height: calc(90vh - 6rem);">
        <div class="flex justify-between items-center mb-2">
          <h2 class="text-2xl font-bold">Edit Training Program</h2>
          <button on:click={close} class="text-gray-400 hover:text-white text-2xl font-bold">&times;</button>
        </div>
        <input bind:value={title} type="text" placeholder="Program Title" class="input input-bordered w-full" />
        <input bind:value={startDate} type="date" class="input input-bordered w-full" />
        <input bind:value={endDate} type="date" class="input input-bordered w-full" />
        <div class="flex flex-wrap gap-2">
          <label class="cursor-pointer flex items-center gap-1" for="tag-select">
            <span class="font-semibold">Tag:</span>
            <select
              id="tag-select"
              bind:value={selectedTag}
              class="select select-bordered"
            >
              {#each tagOptions as tag}
                <option value={tag}>{tag}</option>
              {/each}
            </select>
          </label>
        </div>
        <div class="flex items-center gap-2 mt-2">
          <span class="font-semibold">Status:</span>
          <select bind:value={isCompleted} class="select select-bordered">
            <option value={false}>Active</option>
            <option value={true}>Complete</option>
          </select>
        </div>
      </div>
      <div class="p-4 border-t border-base-300 bg-base-100 flex justify-between">
        <button on:click={close} class="btn btn-ghost">Cancel</button>
        <button on:click={saveEdits} class="btn btn-success">Save</button>
      </div>
    </div>
  </div>
{/if}
