<script lang="ts">
  import { onMount } from 'svelte';
  import { goto } from '$app/navigation';
  import {
    MovementBase,
    CreateMovementBaseEndpointClient,
    CreateMovementBaseRequest
  } from '$lib/api/ApiClient';

  let movementOptions: MovementBase[] = [];
  let newMovementName = '';
  let error = '';

  async function loadMovements() {
    try {
      const res = await fetch('http://localhost:5174/api/movement-base/get-all', {
        credentials: 'include'
      });
      movementOptions = await res.json();
    } catch (err) {
      console.error('Failed to fetch movement bases:', err);
    }
  }

  async function addMovementBase() {
  error = '';
  if (!newMovementName.trim()) return;

  try {
    const res = await fetch('http://localhost:5174/api/movement-base/create', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json'
      },
      credentials: 'include',
      body: JSON.stringify({ name: newMovementName })
    });

    if (!res.ok) throw new Error('Bad response');

    newMovementName = '';
    await loadMovements();
  } catch (e) {
    error = 'Failed to add movement base';
    console.error(e);
  }
}

  onMount(loadMovements);
</script>

<div class="p-6 max-w-4xl mx-auto text-white">
  <button on:click={() => goto('/programs')} class="mb-6 text-sm bg-zinc-700 hover:bg-zinc-600 px-3 py-1 rounded">
    ← Back to Sessions
  </button>

  <h1 class="text-3xl font-bold mb-6">Movement Library</h1>

  <div class="mb-6">
    <h2 class="text-xl font-semibold mb-2">Add New Movement</h2>
    <div class="flex gap-2">
      <input
        type="text"
        placeholder="Movement name"
        bind:value={newMovementName}
        class="p-2 bg-zinc-800 border border-zinc-600 rounded w-full"
      />
      <button on:click={addMovementBase} class="px-4 py-2 bg-blue-600 hover:bg-blue-500 rounded">Add</button>
    </div>
    {#if error}
      <p class="text-red-400 mt-2">{error}</p>
    {/if}
  </div>

  <div class="mt-6">
    <h2 class="text-xl font-semibold mb-4">Available Movements</h2>
    <ul class="space-y-2">
      {#each movementOptions as m}
        <li class="bg-zinc-800 border border-zinc-600 p-3 rounded">{m.name}</li>
      {/each}
    </ul>
  </div>
</div>
