<script lang="ts">
  import { createEventDispatcher, onMount } from 'svelte';
  import { dndzone, type DndEvent } from 'svelte-dnd-action';
  import {
    MovementBase,
    MovementModifier,
    WeightUnit,
    CreateMovementEndpointClient,
    CreateSetEntryEndpointClient,
    CreateTrainingSessionEndpointClient,
    CreateTrainingSessionRequest,
    CreateMovementRequest,
    CreateSetEntryRequest,
    UpdateMovementOrderEndpointClient,
    UpdateMovementOrderRequest,
    MovementDTO
  } from '$lib/api/ApiClient';

  export let show: boolean;
  export let programID: string;
  export let existingSessionCount: number = 0;

  const dispatch = createEventDispatcher();
  const baseUrl = typeof window !== 'undefined'
    ? window.location.origin
    : 'http://localhost:5174';

  let movementOptions: MovementBase[] = [];
  let selectedMovementBaseID = '';
  let tempModifierID = 'No Modifier';
  let repSchemeDraft = { sets: 1, reps: 5, rpe: 8 };
  let movementModifierName='';
  let movementModifierEquipment = '';
  let movementModifierDuration = 0;

  interface MovementItem {
    movementID: string;
    movementBaseID: string;
    name: string;
    modifierID?: string;
    modifierName?: string;
    repSchemes: { sets: number; reps: number; rpe: number }[];
    movementDTO?: MovementDTO;
  }

  let movements: MovementItem[] = [];

  const modifiers: MovementModifier[] = [
    MovementModifier.fromJS({ name: 'No Modifier', equipment: 'None', duration: 0 }),
    MovementModifier.fromJS({ name: 'Paused', equipment: 'Barbell', duration: 1 }),
    MovementModifier.fromJS({ name: 'Tempo 3-1-0', equipment: 'Barbell', duration: 1 }),
    MovementModifier.fromJS({ name: 'Dumbbell Variant', equipment: 'Dumbbell', duration: 0 }),
    MovementModifier.fromJS({ name: 'Chains Added', equipment: 'Barbell', duration: 0 })
  ];

  onMount(async () => {
    try {
      const res = await fetch(
        `${baseUrl}/api/movement-base/get-all`,
        { credentials: 'include' }
      );
      movementOptions = await res.json();
    } catch (err) {
      console.error('Failed to fetch movement bases:', err);
    }
  });

  function generateGuid(): string {
    return crypto.randomUUID();
  }

  function addMovementByID() {
    const base = movementOptions.find(
      (b) => b.movementBaseID === selectedMovementBaseID
    );
    if (!base) return;
    const matchedMod = modifiers.find((m) => m.name === tempModifierID);
    const guid = generateGuid();
    movements = [
      ...movements,
      {
        movementID: guid,
        movementBaseID: base.movementBaseID!,
        name: base.name!,
        modifierID: matchedMod?.name,
        modifierName: matchedMod?.name,
        repSchemes: [{ ...repSchemeDraft }]
      }
    ];
    selectedMovementBaseID = '';
  }

  function addRepScheme(index: number) {
    movements[index].repSchemes.push({ sets: 1, reps: 5, rpe: 8 });
    movements = [...movements];
  }

  function removeMovement(index: number) {
    movements = movements.filter((_, i) => i !== index);
  }

  function removeRepScheme(mvIndex: number, rsIndex: number) {
    movements[mvIndex].repSchemes.splice(rsIndex, 1);
    movements = [...movements];
  }

  function handleDnd(event: CustomEvent<DndEvent>) {
    movements = event.detail.items as MovementItem[];
  }

  async function createSession() {
    const sessionClient = new CreateTrainingSessionEndpointClient(baseUrl);
    const movementClient = new CreateMovementEndpointClient(baseUrl);
    const setClient = new CreateSetEntryEndpointClient(baseUrl);
    const orderClient = new UpdateMovementOrderEndpointClient(baseUrl);

    const sessionDate = new Date();
    sessionDate.setDate(sessionDate.getDate() + 2);

    const sessionRequest = CreateTrainingSessionRequest.fromJS({
      trainingProgramID: programID,
      date: sessionDate
    });

    const session = await sessionClient.create5(sessionRequest);
    const tempToReal: Record<string, string> = {};

    for (const movement of movements) {
      const matchedMod = modifiers.find(
        (m) => m.name === movement.modifierID
      )!;
      const movementReq = CreateMovementRequest.fromJS({
        movementBaseID: movement.movementBaseID,
        trainingSessionID: session.trainingSessionID!,
        notes: '',
        movementModifier: new MovementModifier({
          name: movementModifierName,
          equipment: movementModifierEquipment,
          duration: movementModifierDuration
        })
      });
      movementModifierName = '';
      movementModifierEquipment = '';
      movementModifierDuration = 0;
      const movementDTO = await movementClient.create2(movementReq);
      movement.movementDTO = movementDTO;
      tempToReal[movement.movementID] = movementDTO.movementID!;

      for (const scheme of movement.repSchemes) {
        for (let i = 0; i < scheme.sets; i++) {
          const setReq = CreateSetEntryRequest.fromJS({
            movementID: movementDTO.movementID!,
            recommendedReps: scheme.reps,
            recommendedWeight: 0,
            recommendedRPE: scheme.rpe,
            weightUnit: WeightUnit._1,
            actualReps: scheme.reps,
            actualWeight: 0,
            actualRPE: scheme.rpe
          });
          await setClient.create3(setReq);
        }
      }
    }

    await orderClient.updateOrder(
      UpdateMovementOrderRequest.fromJS({
        trainingSessionID: session.trainingSessionID!,
        iDs: movements.map((m) => tempToReal[m.movementID])
      })
    );

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
    <div class="bg-base-200 text-base-content rounded-lg w-full max-w-2xl border border-base-300 max-h-[90vh] flex flex-col">
      <div class="p-6 overflow-y-auto" style="max-height: calc(90vh - 4rem);">
        <div class="flex justify-between items-center mb-4">
          <h2 class="text-2xl font-bold">Create New Training Session</h2>
          <button on:click={close} class="text-gray-400 hover:text-white text-2xl font-bold">&times;</button>
        </div>

        <div class="flex flex-col">
          <select bind:value={selectedMovementBaseID} class="select select-bordered w-full">
            <option value="">Select a movement</option>
            {#each movementOptions as m}
              <option value={m.movementBaseID}>{m.name}</option>
            {/each}
          </select>

          <div class="flex flex-col">
                <input
                  type="text"
                  placeholder="Type here"
                  class="input input w-32 m-1"
                  bind:value={movementModifierName}

                />

                <select
                  class="select w-32 m-1"
                  bind:value={movementModifierEquipment}

                >
                  <option disabled selected
                    >{movementModifierEquipment}</option
                  >
                  <option>Kettlebell</option>
                  <option>Barbell</option>
                  <option>Angular</option>
                </select>

                <input
                  type="number"
                  id="duration"
                  min="0"
                  bind:value={movementModifierDuration}
                  
                  class="peer input input-xl y w-16 m-1"
                />
              </div>
        </div>

        <button on:click={addMovementByID} class="text-sm text-green-500 hover:underline mb-6">+ Add Movement</button>

        {#if movements.length}
          <div class="space-y-4">
            <div
              use:dndzone={{ items: movements }}
              on:consider={handleDnd}
              on:finalize={handleDnd}
            >
              {#each movements as m, i (m.movementID)}
                <div class="border border-base-300 p-4 rounded bg-base-100" data-id={m.movementID}>
                  <div class="flex justify-between items-center mb-2">
                    <strong class="text-lg">
                      {m.name}
                      {#if m.modifierName}
                        – <span class="italic text-sm text-gray-400">{m.modifierName}</span>
                      {/if}
                    </strong>
                    <button on:click={() => removeMovement(i)} class="text-red-500 hover:underline text-sm">Remove</button>
                  </div>

                  <div class="space-y-2">
                    {#each m.repSchemes as rs, j}
                      <div class="grid grid-cols-3 gap-4 text-sm font-semibold text-gray-400 mb-1">
                        <div class="text-center">Sets</div>
                        <div class="text-center">Reps</div>
                        <div class="text-center">RPE</div>

                        <input type="number" min="1" bind:value={rs.sets} class="input input-sm input-bordered text-center" />
                        <input type="number" min="1" bind:value={rs.reps} class="input input-sm input-bordered text-center" />
                        <input type="number" step="0.5" min="1" max="10" bind:value={rs.rpe} class="input input-sm input-bordered text-center" />
                        <button on:click={() => removeRepScheme(i, j)} class="text-xs text-red-400 hover:underline">Remove Scheme</button>
                      </div>
                    {/each}
                    <button on:click={() => addRepScheme(i)} class="text-sm text-green-500 hover:underline mt-2">+ Add Rep Scheme</button>
                  </div>
                </div>
              {/each}
            </div>
          </div>
        {/if}
      </div>

      <div class="p-4 border-t border-base-300 bg-base-100 flex justify-end space-x-2">
        <button on:click={close} class="btn btn-ghost">Cancel</button>
        <button on:click={createSession} class="btn btn-primary">Create Session</button>
      </div>
    </div>
  </div>
{/if}
