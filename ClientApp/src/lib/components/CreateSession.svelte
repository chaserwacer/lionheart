<script lang="ts">
  import { createEventDispatcher } from 'svelte';
  import {
    CreateTrainingSessionEndpointClient,
    CreateTrainingSessionRequest
  } from '$lib/api/ApiClient';

  export let show: boolean;
  export let programID: string;
  export let existingSessionCount: number = 0;

  const dispatch = createEventDispatcher();
  const baseUrl = typeof window !== 'undefined'
    ? window.location.origin
    : 'http://localhost:5174';

  let sessionDate: string = new Date().toISOString().slice(0, 10);

  async function createSession() {
    const sessionClient = new CreateTrainingSessionEndpointClient(baseUrl);
    var date = new Date(sessionDate);
    date.setDate(date.getDate() + 1);
    const sessionRequest = CreateTrainingSessionRequest.fromJS({
      trainingProgramID: programID,
      date: date
    });

    const session = await sessionClient.create7(sessionRequest);

    dispatch('createdWithSession', {
      sessionID: session.trainingSessionID!,
      sessionNumber: existingSessionCount + 1,
      date: session.date
    });

    close();
  }

  function close() {
    dispatch('close');
  }
</script>

{#if show}
  <div class="fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center">
    <div class="bg-base-200 text-base-content rounded-lg w-full max-w-md border border-base-300 max-h-[90vh] flex flex-col">
      <div class="p-6 overflow-y-auto" style="max-height: calc(90vh - 4rem);">
        <div class="flex justify-between items-center mb-4">
          <h2 class="text-2xl font-bold">Create New Training Session</h2>
          <button on:click={close} class="text-gray-400 hover:text-white text-2xl font-bold">&times;</button>
        </div>
        <div class="flex flex-col gap-4">
          <label class="font-semibold" for="session-date">Session Date</label>
          <input
            id="session-date"
            type="date"
            class="input input-bordered w-full"
            bind:value={sessionDate}
          />
        </div>
      </div>
      <div class="p-4 border-t border-base-300 bg-base-100 flex justify-end space-x-2">
        <button on:click={close} class="btn btn-ghost">Cancel</button>
        <button on:click={createSession} class="btn btn-primary">Create Session</button>
      </div>
    </div>
  </div>
{/if}
