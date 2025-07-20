<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import { slugify } from "$lib/utils/slugify";
  import { page } from "$app/stores";
  import {
    MovementBase,
    CreateMovementBaseRequest,
    CreateMovementBaseEndpointClient,
    DeleteMovementBaseEndpointClient,
    GetMovementBasesEndpointClient,
  } from "$lib/api/ApiClient";
  import {
    Equipment,
    CreateEquipmentRequest,
    CreateEquipmentEndpointClient,
    DeleteEquipmentEndpointClient,
    GetEquipmentsEndpointClient,
  } from "$lib/api/ApiClient";

  let movementOptions: MovementBase[] = [];
  let newMovementName = "";
  let error = "";
  const baseUrl =
    typeof window !== "undefined"
      ? window.location.origin
      : "http://localhost:5174";

  const deleteClient = new DeleteMovementBaseEndpointClient(baseUrl);
  async function deleteMovementBase(id: string) {
    if (id === "") {
      error = "Invalid movement base ID";
      return;
    }
    error = "";
    try {
      await deleteClient.delete2(id);
      await loadMovements();
    } catch (e: any) {
      if (e instanceof Response) {
        if (e.status === 409) {
          error = "Cannot delete: Movement base is in use.";
        } else if (e.status === 404) {
          error = "Movement base not found or not owned by you.";
        } else {
          error = "Failed to delete movement base.";
        }
      } else {
        error = "Failed to delete movement base";
      }
      console.error(e);
    }
  }

  const getAllClient = new GetMovementBasesEndpointClient(baseUrl);
  async function loadMovements() {
    try {
      movementOptions = await getAllClient.getAll2();
    } catch (err) {
      console.error("Failed to fetch movement bases:", err);
    }
  }

  const createClient = new CreateMovementBaseEndpointClient(baseUrl);
  async function addMovementBase() {
    error = "";
    if (!newMovementName.trim()) return;

    try {
      const req = new CreateMovementBaseRequest();
      req.name = newMovementName;
      await createClient.create2(req);
      newMovementName = "";
      await loadMovements();
    } catch (e) {
      error = "Failed to add movement base";
      console.error(e);
    }
  }

  let equipmentOptions: Equipment[] = [];
  let newEquipmentName = "";

  let equipmentError = "";
  const equipmentBaseUrl =
    typeof window !== "undefined"
      ? window.location.origin
      : "http://localhost:5174";

  const deleteEquipmentClient = new DeleteEquipmentEndpointClient(
    equipmentBaseUrl,
  );
  async function deleteEquipment(id: string) {
    if (id === "") {
      equipmentError = "Invalid equipment ID";
      return;
    }
    equipmentError = "";
    try {
      await deleteEquipmentClient.delete(id);
      await loadEquipments();
    } catch (e: any) {
      if (e instanceof Response) {
        if (e.status === 409) {
          equipmentError = "Cannot delete: Equipment is in use.";
        } else if (e.status === 404) {
          equipmentError = "Equipment not found or not owned by you.";
        } else {
          equipmentError = "Failed to delete equipment.";
        }
      } else {
        equipmentError = "Failed to delete equipment";
      }
      console.error(e);
    }
  }

  const getAllEquipmentClient = new GetEquipmentsEndpointClient(
    equipmentBaseUrl,
  );
  async function loadEquipments() {
    try {
      equipmentOptions = await getAllEquipmentClient.getAll();
    } catch (err) {
      console.error("Failed to fetch equipment:", err);
    }
  }

  const createEquipmentClient = new CreateEquipmentEndpointClient(
    equipmentBaseUrl,
  );
  async function addEquipment() {
    equipmentError = "";
    if (!newEquipmentName.trim()) return;

    try {
      const req = new CreateEquipmentRequest();
      req.name = newEquipmentName;
      await createEquipmentClient.create(req);
      newEquipmentName = "";
      await loadEquipments();
    } catch (e) {
      equipmentError = "Failed to add equipment";
      console.error(e);
    }
  }

  $: programSlug = $page.params.slug;

  function goToPreviousPage() {
    if ($page.params.sessionID) {
      goto(`/programs/${programSlug}/session/${$page.params.sessionID}`);

      return;
    }
    goto(`/programs/${programSlug}`);
  }

  $: returnTo = $page.url.searchParams.get("returnTo") ?? "/programs";

  onMount(() => {
    loadMovements();
    loadEquipments();
  });
</script>

<div class="p-6 max-w-4xl mx-auto text-base-content">
  <a class="btn btn-sm btn-outline mb-4" href={returnTo}>← Back</a>

  <h1 class="text-3xl font-bold mb-6">Item Library</h1>

  <div class="flex flex-col md:flex-row">
    <!-- Add New Movement -->
    <div class="flex flex-col">
      <div class="mb-6">
        <h2 class="text-xl font-semibold mb-2">Add New Movement</h2>
        <div class="flex gap-2">
          <input
            type="text"
            placeholder="Movement name"
            bind:value={newMovementName}
            class="input input-bordered w-full"
          />
          <button on:click={addMovementBase} class="btn btn-primary">Add</button
          >
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
            <li
              class="bg-base-200 border border-base-300 p-3 rounded flex items-center justify-between"
            >
              <span>{m.name}</span>
              {#if m.userID}
                <button
                  class="btn btn-xs btn-error"
                  on:click={() => deleteMovementBase(m.movementBaseID ?? "")}
                  >Delete</button
                >
              {/if}
            </li>
          {/each}
        </ul>
      </div>
    </div>
<div class="divider divider-vertical md:divider-horizontal"></div>

    <!-- Add New Equipment -->
    <div class="flex flex-col">
      <div class="mb-6">
        <h2 class="text-xl font-semibold mb-2">Add New Equipment</h2>
        <div class="flex gap-2">
          <input
            type="text"
            placeholder="Equipment name"
            bind:value={newEquipmentName}
            class="input input-bordered w-full"
          />
          <button on:click={addEquipment} class="btn btn-primary">Add</button>
        </div>
        {#if equipmentError}
          <p class="text-error mt-2">{equipmentError}</p>
        {/if}
      </div>

      <!-- List of Equipment -->
      <div class="mt-6">
        <h2 class="text-xl font-semibold mb-4">Available Equipment</h2>
        <ul class="space-y-2">
          {#each equipmentOptions as e}
            <li
              class="bg-base-200 border border-base-300 p-3 rounded flex items-center justify-between"
            >
              <span>{e.name}</span>
              {#if e.userID}
                <button
                  class="btn btn-xs btn-error"
                  on:click={() => deleteEquipment(e.equipmentID ?? "")}
                  >Delete</button
                >
              {/if}
            </li>
          {/each}
        </ul>
      </div>
    </div>
  </div>
</div>
