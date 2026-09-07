<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { describe, type Result } from "$lib/api/result";
  import {
    activateProgram,
    completeProgram,
    deleteProgram,
    getProgram,
    LIFECYCLE_BADGES,
    LIFECYCLE_LABELS,
    PROGRAM_LIFECYCLE_LABELS,
    updateProgram,
    type TrainingProgramView,
  } from "$lib/api/training";
  import { relativeDay, training } from "$lib/stores/training.svelte";
  import CreateTrainingSessionModal from "$lib/components/modals/CreateTrainingSessionModal.svelte";

  const programId = $derived(page.params.programId ?? "");

  let program = $state<TrainingProgramView | null>(null);
  let errorMsg = $state("");
  let busy = $state(false);

  let editing = $state(false);
  let draftTitle = $state("");
  let draftStart = $state("");
  let draftEnd = $state("");
  let draftTags = $state("");
  let summary = $state("");

  $effect(() => {
    if (programId !== "") load();
  });

  $effect(() => {
    if (training.sessions.length === 0) training.loadSessions();
  });

  const sessions = $derived(
    training.sessions
      .filter((session) => session.trainingProgramId === programId)
      .sort((a, b) => a.date.localeCompare(b.date)),
  );

  const completedCount = $derived(
    sessions.filter((session) => session.$type === "completed").length,
  );

  async function load() {
    const result = await getProgram(programId);
    if (result.ok) {
      program = result.value;
      errorMsg = "";
    } else {
      errorMsg = describe(result.problem);
    }
  }

  function startEditing() {
    if (program === null) return;
    draftTitle = program.title;
    draftStart = program.startDate;
    draftEnd = program.endDate;
    draftTags = program.tags.join(", ");
    editing = true;
  }

  async function apply(call: () => Promise<Result<TrainingProgramView>>) {
    busy = true;
    const result = await call();
    busy = false;

    if (result.ok) {
      program = result.value;
      errorMsg = "";
      training.loadPrograms();
    } else {
      errorMsg = describe(result.problem);
    }
  }

  function saveEdits() {
    return apply(async () => {
      const result = await updateProgram(programId, {
        title: draftTitle,
        startDate: draftStart,
        endDate: draftEnd,
        tags: draftTags
          .split(",")
          .map((tag) => tag.trim())
          .filter((tag) => tag !== ""),
      });
      if (result.ok) editing = false;
      return result;
    });
  }

  async function remove() {
    busy = true;
    const result = await deleteProgram(programId);
    busy = false;

    if (result.ok) {
      training.loadPrograms();
      goto("/training");
    } else {
      errorMsg = describe(result.problem);
    }
  }
</script>

<svelte:head>
  <title>Program - Lionheart</title>
</svelte:head>

<div class="min-h-full bg-base-200">
  <div class="mx-auto max-w-3xl px-4 py-6 pb-24 sm:px-6 sm:py-10">
    <a
      href="/training"
      class="mb-6 inline-block font-mono text-xs uppercase tracking-widest text-base-content/50"
    >
      &larr; Training
    </a>

    {#if errorMsg}
      <p class="mb-4 rounded-xl border-2 border-error/40 bg-error/10 px-4 py-3 text-sm text-error">
        {errorMsg}
      </p>
    {/if}

    {#if program === null}
      <p class="text-sm text-base-content/50">Loading…</p>
    {:else}
      <header class="mb-6">
        <div class="flex items-start justify-between gap-3">
          <h1 class="font-display text-4xl font-black leading-none tracking-tightest">
            {program.title}
          </h1>
          <span class="badge shrink-0 badge-outline">
            {PROGRAM_LIFECYCLE_LABELS[program.$type]}
          </span>
        </div>
        <p class="mt-3 font-mono text-sm uppercase tracking-widest text-base-content/50">
          {program.startDate} → {program.endDate}
        </p>
        {#if program.tags.length > 0}
          <div class="mt-3 flex flex-wrap gap-1.5">
            {#each program.tags as tag (tag)}
              <span class="badge badge-outline badge-sm font-mono text-[0.625rem]">{tag}</span>
            {/each}
          </div>
        {/if}
        {#if program.$type === "completed"}
          <p class="mt-4 text-sm text-base-content/70">{program.summary}</p>
        {/if}
      </header>

      <div class="mb-8 flex flex-wrap gap-2">
        {#if program.$type === "planned"}
          <button
            onclick={() => apply(() => activateProgram(programId))}
            disabled={busy}
            class="btn btn-neutral min-h-11 h-11 rounded-xl px-5 text-xs font-bold uppercase tracking-widest"
          >
            Activate
          </button>
        {:else if program.$type === "active"}
          <button
            onclick={() => apply(() => completeProgram(programId, summary))}
            disabled={busy}
            class="btn btn-neutral min-h-11 h-11 rounded-xl px-5 text-xs font-bold uppercase tracking-widest"
          >
            Complete
          </button>
        {/if}

        <button
          onclick={() => (editing ? (editing = false) : startEditing())}
          class="btn btn-outline min-h-11 h-11 rounded-xl border-2 px-5 text-xs font-bold uppercase tracking-widest"
        >
          {editing ? "Cancel" : "Edit"}
        </button>

        <button
          onclick={() =>
            (document.getElementById("program_session_modal") as HTMLDialogElement | null)?.showModal()}
          class="btn btn-ghost min-h-11 h-11 text-xs font-bold uppercase tracking-widest"
        >
          Add session
        </button>

        <button
          onclick={remove}
          class="btn btn-ghost min-h-11 h-11 text-xs font-bold uppercase tracking-widest text-error"
        >
          Delete
        </button>
      </div>

      {#if program.$type === "active"}
        <label class="label" for="program-summary">
          <span class="label-text text-xs font-bold uppercase tracking-widest">
            Summary, for when it is done
          </span>
        </label>
        <textarea
          id="program-summary"
          bind:value={summary}
          rows="2"
          class="textarea textarea-bordered mb-8 w-full"
        ></textarea>
      {/if}

      {#if editing}
        <section class="mb-8 rounded-2xl border-2 border-base-content/10 bg-base-100 p-5">
          <label class="label" for="program-title">
            <span class="label-text text-xs font-bold uppercase tracking-widest">Title</span>
          </label>
          <input id="program-title" bind:value={draftTitle} class="input input-bordered w-full" />

          <div class="mt-4 grid grid-cols-2 gap-3">
            <div>
              <label class="label" for="program-start">
                <span class="label-text text-xs font-bold uppercase tracking-widest">Starts</span>
              </label>
              <input
                id="program-start"
                type="date"
                bind:value={draftStart}
                class="input input-bordered w-full"
              />
            </div>
            <div>
              <label class="label" for="program-end">
                <span class="label-text text-xs font-bold uppercase tracking-widest">Ends</span>
              </label>
              <input
                id="program-end"
                type="date"
                bind:value={draftEnd}
                class="input input-bordered w-full"
              />
            </div>
          </div>

          <label class="label mt-4" for="program-tags">
            <span class="label-text text-xs font-bold uppercase tracking-widest">Tags</span>
          </label>
          <input id="program-tags" bind:value={draftTags} class="input input-bordered w-full" />

          <button
            onclick={saveEdits}
            disabled={busy}
            class="btn btn-neutral mt-4 min-h-11 h-11 rounded-xl px-5 text-xs font-bold uppercase tracking-widest"
          >
            Save
          </button>
        </section>
      {/if}

      <section>
        <h2 class="mb-1 font-display text-2xl font-black tracking-tight">Sessions</h2>
        <p class="mb-4 font-mono text-xs uppercase tracking-widest text-base-content/50">
          {completedCount} of {sessions.length} completed in the loaded window
        </p>

        {#if sessions.length === 0}
          <p class="text-sm text-base-content/50">No sessions in this program yet.</p>
        {:else}
          <div class="flex flex-col gap-2">
            {#each sessions as session (session.trainingSessionId)}
              <a
                href="/training/session/{session.trainingSessionId}"
                class="flex items-center gap-4 rounded-xl border-2 border-base-content/10 bg-base-100
                       px-5 py-4 transition-colors hover:border-base-content/30"
              >
                <span class="badge {LIFECYCLE_BADGES[session.$type]} badge-sm shrink-0">
                  {LIFECYCLE_LABELS[session.$type]}
                </span>
                <span class="min-w-0 flex-1">
                  <span class="block truncate font-bold">
                    {session.notes?.trim() || "Training session"}
                  </span>
                  <span
                    class="mt-0.5 block font-mono text-xs uppercase tracking-widest text-base-content/50"
                  >
                    {relativeDay(session.date)}
                  </span>
                </span>
                <span class="shrink-0 text-base-content/30">&rarr;</span>
              </a>
            {/each}
          </div>
        {/if}
      </section>
    {/if}
  </div>
</div>

<CreateTrainingSessionModal
  modalId="program_session_modal"
  trainingProgramId={programId}
  oncreated={() => training.loadSessions()}
/>
