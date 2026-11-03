<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { onDestroy } from "svelte";
  import {
    LIFECYCLE_BADGES,
    LIFECYCLE_LABELS,
    prescriptionsOf,
    setsOf,
    WEIGHT_UNIT_LABELS,
    type SetCommand,
    type SetPrescriptionCommand,
    type WeightUnit,
  } from "$lib/api/training";
  import { sessionDetail } from "$lib/stores/session.svelte";
  import { relativeDay, training } from "$lib/stores/training.svelte";
  import { describePrescription, describeSet, facetLabel } from "$lib/utils/setFormat";

  const sessionId = $derived(page.params.sessionId ?? "");

  // Reload whenever the route's id changes, not only on first mount.
  $effect(() => {
    if (sessionId !== "") sessionDetail.load(sessionId);
  });

  $effect(() => {
    if (training.movementDatas.length === 0) training.loadLibrary();
  });

  onDestroy(() => sessionDetail.reset());

  const session = $derived(sessionDetail.session);
  const nameOf = (id: string) => training.movementName(id);

  // A new set needs a movement, and the library is the only place ids come from.
  let pickedMovement = $state("");
  let reps = $state(5);
  let weight = $state(100);
  let unit = $state<WeightUnit>(1);
  let freeformText = $state("");

  const facets = $derived(
    session?.$type === "completed" && session.decoration.$type === "decorated"
      ? session.decoration.facets
      : [],
  );

  function addStructured() {
    if (pickedMovement === "") return;
    if (sessionDetail.editable === "prescriptions") {
      sessionDetail.addLiftPrescription(pickedMovement, reps, weight, unit);
    } else {
      sessionDetail.addLiftSet(pickedMovement, reps, weight, unit);
    }
  }

  function addFreeform() {
    if (freeformText.trim() === "") return;
    if (sessionDetail.editable === "prescriptions") {
      sessionDetail.addFreeformPrescription(freeformText.trim());
    } else {
      sessionDetail.addFreeformSet(freeformText.trim());
    }
    freeformText = "";
  }

  async function remove() {
    if (await sessionDetail.remove()) goto("/training");
  }
</script>

<svelte:head>
  <title>Session - Lionheart</title>
</svelte:head>

<div class="min-h-full bg-base-200">
  <div class="mx-auto max-w-3xl px-4 py-6 pb-24 sm:px-6 sm:py-10">
    <a
      href="/training"
      class="mb-6 inline-block font-mono text-xs uppercase tracking-widest text-base-content/50"
    >
      &larr; Training
    </a>

    {#if sessionDetail.loading}
      <p class="text-sm text-base-content/50">Loading…</p>
    {:else if session === null}
      <p class="text-sm text-base-content/50">That session is not here.</p>
    {:else}
      <header class="mb-6">
        <div class="flex items-start justify-between gap-3">
          <h1 class="font-display text-4xl font-black leading-none tracking-tightest">
            {session.notes?.trim() || "Session"}
          </h1>
          <span class="badge {LIFECYCLE_BADGES[session.$type]} shrink-0">
            {LIFECYCLE_LABELS[session.$type]}
          </span>
        </div>
        <p class="mt-3 font-mono text-sm uppercase tracking-widest text-base-content/50">
          {relativeDay(session.$type === "planned" || session.$type === "skipped"
            ? session.plannedFor
            : session.date)}
        </p>
      </header>

      {#if sessionDetail.error}
        <p class="mb-4 rounded-xl border-2 border-error/40 bg-error/10 px-4 py-3 text-sm text-error">
          {sessionDetail.error}
        </p>
      {/if}

      <!-- Transitions, not a status field. Each is offered only from the state it is legal in. -->
      <div class="mb-8 flex flex-wrap gap-2">
        {#if session.$type === "planned"}
          <button
            onclick={() => sessionDetail.start()}
            disabled={sessionDetail.saving}
            class="btn btn-neutral min-h-11 h-11 rounded-xl px-5 text-xs font-bold uppercase tracking-widest"
          >
            Start
          </button>
          <button
            onclick={() => sessionDetail.skip()}
            disabled={sessionDetail.saving}
            class="btn btn-outline min-h-11 h-11 rounded-xl border-2 px-5 text-xs font-bold uppercase tracking-widest"
          >
            Skip
          </button>
        {:else if session.$type === "active"}
          <button
            onclick={() => sessionDetail.complete()}
            disabled={sessionDetail.saving}
            class="btn btn-neutral min-h-11 h-11 rounded-xl px-5 text-xs font-bold uppercase tracking-widest"
          >
            Complete
          </button>
        {/if}

        {#if sessionDetail.editable !== "none"}
          {#if sessionDetail.editing}
            <button
              onclick={() => sessionDetail.save()}
              disabled={sessionDetail.saving}
              class="btn btn-neutral min-h-11 h-11 rounded-xl px-5 text-xs font-bold uppercase tracking-widest"
            >
              Save
            </button>
            <button
              onclick={() => sessionDetail.cancelEditing()}
              class="btn btn-ghost min-h-11 h-11 text-xs font-bold uppercase tracking-widest"
            >
              Cancel
            </button>
          {:else}
            <button
              onclick={() => sessionDetail.startEditing()}
              class="btn btn-outline min-h-11 h-11 rounded-xl border-2 px-5 text-xs font-bold uppercase tracking-widest"
            >
              Edit
            </button>
          {/if}
        {/if}

        <button
          onclick={remove}
          class="btn btn-ghost min-h-11 h-11 text-xs font-bold uppercase tracking-widest text-error"
        >
          Delete
        </button>
      </div>

      {#if sessionDetail.editing}
        <section class="mb-6 rounded-2xl border-2 border-base-content/10 bg-base-100 p-5">
          <label class="label" for="session-notes">
            <span class="label-text text-xs font-bold uppercase tracking-widest">Notes</span>
          </label>
          <textarea
            id="session-notes"
            bind:value={sessionDetail.draftNotes}
            rows="2"
            class="textarea textarea-bordered w-full"
          ></textarea>

          <h2 class="mt-6 font-display text-xl font-black tracking-tight">
            {sessionDetail.editable === "prescriptions" ? "Prescribed" : "Sets"}
          </h2>
          <p class="mt-1 text-xs text-base-content/40">
            The whole list is written at once — there is no per-set save.
          </p>

          <ul class="mt-3 flex flex-col gap-2">
            {#each sessionDetail.editable === "prescriptions" ? sessionDetail.draftPrescriptions : sessionDetail.draftSets as item, index (index)}
              <li class="flex items-center gap-2 rounded-xl border border-base-content/10 px-3 py-2">
                <span class="min-w-0 flex-1 truncate text-sm">
                  {sessionDetail.editable === "prescriptions"
                    ? describePrescription(item as SetPrescriptionCommand, nameOf)
                    : describeSet(item as SetCommand, nameOf)}
                </span>
                <button
                  aria-label="Move up"
                  onclick={() =>
                    sessionDetail.editable === "prescriptions"
                      ? sessionDetail.movePrescription(index, -1)
                      : sessionDetail.moveSet(index, -1)}
                  class="btn btn-ghost btn-xs min-h-11 h-11">↑</button
                >
                <button
                  aria-label="Move down"
                  onclick={() =>
                    sessionDetail.editable === "prescriptions"
                      ? sessionDetail.movePrescription(index, 1)
                      : sessionDetail.moveSet(index, 1)}
                  class="btn btn-ghost btn-xs min-h-11 h-11">↓</button
                >
                <button
                  aria-label="Remove"
                  onclick={() =>
                    sessionDetail.editable === "prescriptions"
                      ? sessionDetail.removePrescription(index)
                      : sessionDetail.removeSet(index)}
                  class="btn btn-ghost btn-xs min-h-11 h-11 text-error">✕</button
                >
              </li>
            {/each}
          </ul>

          <div class="mt-5 grid grid-cols-2 gap-2 sm:grid-cols-5">
            <select bind:value={pickedMovement} class="select select-bordered col-span-2 text-sm">
              <option value="">Pick a movement</option>
              {#each training.movementDatas as movement (movement.movementDataID)}
                <option value={movement.movementDataID}>
                  {training.movementName(movement.movementDataID ?? "")}
                </option>
              {/each}
            </select>
            <input
              type="number"
              bind:value={reps}
              min="1"
              aria-label="Reps"
              class="input input-bordered"
            />
            <input
              type="number"
              bind:value={weight}
              min="0"
              aria-label="Weight"
              class="input input-bordered"
            />
            <select bind:value={unit} aria-label="Unit" class="select select-bordered">
              <option value={0}>{WEIGHT_UNIT_LABELS[0]}</option>
              <option value={1}>{WEIGHT_UNIT_LABELS[1]}</option>
            </select>
          </div>
          <button
            onclick={addStructured}
            class="btn btn-outline mt-2 min-h-11 h-11 w-full rounded-xl border-2 text-xs font-bold uppercase tracking-widest"
          >
            Add set
          </button>

          <div class="mt-4 flex gap-2">
            <input
              type="text"
              bind:value={freeformText}
              placeholder="Or describe it in words"
              class="input input-bordered flex-1"
            />
            <button onclick={addFreeform} class="btn btn-ghost min-h-11 h-11">Add</button>
          </div>
        </section>
      {:else}
        <section class="rounded-2xl border-2 border-base-content/10 bg-base-100 p-5">
          {#if session.$type === "planned"}
            <h2 class="font-display text-xl font-black tracking-tight">Prescribed</h2>
            {#if prescriptionsOf(session).length === 0}
              <p class="mt-3 text-sm text-base-content/50">Nothing prescribed.</p>
            {:else}
              <ul class="mt-3 space-y-2">
                {#each prescriptionsOf(session) as prescription, index (index)}
                  <li class="text-sm text-base-content/80">
                    • {describePrescription(prescription, nameOf)}
                  </li>
                {/each}
              </ul>
            {/if}
          {:else if session.$type === "skipped"}
            <p class="text-sm text-base-content/50">Skipped — nothing was logged.</p>
          {:else}
            <h2 class="font-display text-xl font-black tracking-tight">Sets</h2>
            {#if setsOf(session).length === 0}
              <p class="mt-3 text-sm text-base-content/50">No sets yet.</p>
            {:else}
              <ul class="mt-3 space-y-2">
                {#each setsOf(session) as set, index (index)}
                  <li class="text-sm text-base-content/80">• {describeSet(set, nameOf)}</li>
                {/each}
              </ul>
            {/if}
          {/if}

          {#if session.notes}
            <p class="mt-5 border-t-2 border-base-content/10 pt-4 text-sm text-base-content/60">
              {session.notes}
            </p>
          {/if}

          {#if facets.length > 0}
            <div class="mt-4 flex flex-wrap gap-1.5">
              {#each facets as facet, index (index)}
                <span class="badge badge-outline badge-sm font-mono text-[0.625rem]">
                  {facetLabel(facet)}
                </span>
              {/each}
            </div>
          {/if}
        </section>
      {/if}
    {/if}
  </div>
</div>
