<script lang="ts">
  import { onMount } from 'svelte';
  import { goto } from '$app/navigation';
  import { slugify } from '$lib/utils/slugify';
  import { page } from '$app/stores';
  import {
    MovementBase,
  } from '$lib/api/ApiClient';

  let movementOptions: MovementBase[] = [];
  let newMovementName = '';
  let error = '';

  // Dynamic base URL based on the environment
  const baseUrl = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5174';

  async function loadMovements() {
    try {
      const res = await fetch(`${baseUrl}/api/movement-base/get-all`, {
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
      const res = await fetch(`${baseUrl}/api/movement-base/create`, {
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

  $: programSlug = $page.params.slug;

  onMount(loadMovements);
</script>

<div class="p-6 max-w-4xl mx-auto text-base-content">
  <button
    on:click={() => goto(`/programs`)}
    class="btn btn-sm btn-outline mb-6"
  >
    ← Back to Session
  </button>

  <h1 class="text-3xl font-bold mb-6">Movement Library</h1>

  <!-- Add New Movement -->
  <div class="mb-6">
    <h2 class="text-xl font-semibold mb-2">Add New Movement</h2>
    <div class="flex gap-2">
      <input
        type="text"
        placeholder="Movement name"
        bind:value={newMovementName}
        class="input input-bordered w-full"
      />
      <button on:click={addMovementBase} class="btn btn-primary">Add</button>
    </div>
    {#if error}
      <p class="text-error mt-2">{error}</p>
    {/if}
  </div>

  <!-- List of Movements -->
  <div class="mt-6">
    <h2 class="text-xl font-semibold mb-4">Available Movements</h2>
    <ul class="space-y-2">
      {#each movementOptions as m}
        <li class="bg-base-200 border border-base-300 p-3 rounded">{m.name}</li>
      {/each}
    </ul>
  </div>
</div>
