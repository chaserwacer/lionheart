<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import {
    LIFECYCLE_BADGES,
    LIFECYCLE_LABELS,
    PROGRAM_LIFECYCLE_LABELS,
    prescriptionsOf,
    setsOf,
    type TrainingProgramView,
    type TrainingSessionView,
  } from "$lib/api/training";
  import { relativeDay, training } from "$lib/stores/training.svelte";
  import CreateTrainingSessionModal from "$lib/components/modals/CreateTrainingSessionModal.svelte";
  import CreateTrainingProgramModal from "$lib/components/modals/CreateTrainingProgramModal.svelte";
  import NextSessionCard from "$lib/components/home/NextSessionCard.svelte";
  import LastSessionCard from "$lib/components/home/LastSessionCard.svelte";

  onMount(() => training.loadAll());

  function openDialog(id: string) {
    (document.getElementById(id) as HTMLDialogElement | null)?.showModal();
  }

  function goToSession(session: TrainingSessionView) {
    goto(`/training/session/${session.trainingSessionId}`);
  }

  function goToProgram(program: TrainingProgramView) {
    goto(`/training/program/${program.trainingProgramId}`);
  }

  // A session's size reads off the list it actually has: targets when planned, sets once it ran.
  function sizeOf(session: TrainingSessionView): string {
    const sets = setsOf(session).length;
    if (sets > 0) return `${sets} ${sets === 1 ? "set" : "sets"}`;

    const prescribed = prescriptionsOf(session).length;
    if (prescribed > 0) return `${prescribed} prescribed`;

    return "No sets";
  }

  function sessionsIn(program: TrainingProgramView): TrainingSessionView[] {
    return training.sessions.filter(
      (session) => session.trainingProgramId === program.trainingProgramId,
    );
  }
</script>

<svelte:head>
  <title>Training - Lionheart</title>
</svelte:head>

<div class="min-h-full bg-base-200">
  <div class="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
    <header class="mb-8 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
      <div>
        <h1
          class="font-display text-5xl font-black leading-none tracking-tightest text-base-content"
        >
          TRAINING
        </h1>
        <p class="mt-3 font-mono text-sm uppercase tracking-widest text-base-content/50">
          {training.sessions.length} sessions in the window
        </p>
      </div>

      <div class="flex flex-wrap gap-2">
        <button
          onclick={() => openDialog("create_session_modal")}
          class="btn btn-neutral min-h-11 h-11 rounded-xl px-5 text-xs font-bold uppercase tracking-widest"
        >
          New session
        </button>
        <button
          onclick={() => openDialog("create_program_modal")}
          class="btn btn-outline min-h-11 h-11 rounded-xl border-2 px-5 text-xs font-bold uppercase tracking-widest"
        >
          New program
        </button>
        <button
          onclick={() => goto("/training/equipment")}
          class="btn btn-ghost min-h-11 h-11 text-xs font-bold uppercase tracking-widest"
        >
          Library
        </button>
      </div>
    </header>

    {#if training.error}
      <p class="mb-6 rounded-xl border-2 border-error/40 bg-error/10 px-4 py-3 text-sm text-error">
        {training.error}
      </p>
    {/if}

    {#if training.activeSession}
      <a
        href="/training/session/{training.activeSession.trainingSessionId}"
        class="mb-8 flex items-center justify-between rounded-2xl border-2 border-warning bg-warning/10 px-6 py-5"
      >
        <span>
          <span class="block text-xs font-bold uppercase tracking-widest text-warning">
            In progress
          </span>
          <span class="mt-1 block font-display text-2xl font-black tracking-tight">
            {training.activeSession.notes || "Training session"}
          </span>
        </span>
        <span class="text-2xl text-base-content/40">&rarr;</span>
      </a>
    {/if}

    <div class="mb-8 grid grid-cols-1 gap-4 md:grid-cols-2">
      <LastSessionCard session={training.lastCompletedSession} />
      <NextSessionCard session={training.nextSession} />
    </div>

    <section class="mb-10">
      <h2 class="mb-4 font-display text-2xl font-black tracking-tight">Programs</h2>

      {#if training.programs.length === 0}
        <p class="text-sm text-base-content/50">No programs yet.</p>
      {:else}
        <div class="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
          {#each training.programs as program (program.trainingProgramId)}
            <button
              onclick={() => goToProgram(program)}
              class="card w-full border-2 border-base-content/10 bg-base-100 p-5 text-left shadow-editorial
                     transition-all hover:border-base-content/30"
            >
              <div class="flex items-start justify-between gap-2">
                <span class="font-display text-xl font-black tracking-tight">{program.title}</span>
                <span class="badge badge-sm badge-outline shrink-0">
                  {PROGRAM_LIFECYCLE_LABELS[program.$type]}
                </span>
              </div>
              <p class="mt-2 font-mono text-xs uppercase tracking-widest text-base-content/50">
                {program.startDate} → {program.endDate}
              </p>
              <p class="mt-3 text-sm text-base-content/60">
                {sessionsIn(program).length} sessions in the window
              </p>
            </button>
          {/each}
        </div>
      {/if}
    </section>

    <section>
      <h2 class="mb-4 font-display text-2xl font-black tracking-tight">Recent</h2>

      {#if training.recentSessions.length === 0}
        <p class="text-sm text-base-content/50">
          Nothing logged yet — the <a class="link" href="/log">Log screen</a> is the fastest way in.
        </p>
      {:else}
        <div class="flex flex-col gap-2">
          {#each training.recentSessions as session (session.trainingSessionId)}
            <button
              onclick={() => goToSession(session)}
              class="flex w-full items-center gap-4 rounded-xl border-2 border-base-content/10 bg-base-100
                     px-5 py-4 text-left transition-colors hover:border-base-content/30"
            >
              <span class="badge {LIFECYCLE_BADGES[session.$type]} badge-sm shrink-0">
                {LIFECYCLE_LABELS[session.$type]}
              </span>
              <span class="min-w-0 flex-1">
                <span class="block truncate font-bold">
                  {session.notes?.trim() || "Training session"}
                </span>
                <span class="mt-0.5 block font-mono text-xs uppercase tracking-widest text-base-content/50">
                  {relativeDay(session.date)} · {sizeOf(session)}
                </span>
              </span>
              <span class="shrink-0 text-base-content/30">&rarr;</span>
            </button>
          {/each}
        </div>
      {/if}
    </section>
  </div>
</div>

<CreateTrainingSessionModal oncreated={() => training.loadSessions()} />
<CreateTrainingProgramModal oncreated={() => training.loadPrograms()} />
