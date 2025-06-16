<script lang="ts">
  import { page } from '$app/stores';
  import { onMount } from 'svelte';
  import { goto } from '$app/navigation';
  import CreateSessionModal from '$lib/components/CreateSession.svelte';
  import { slugify } from '$lib/utils/slugify';
  import {
    GetTrainingProgramsEndpointClient,
    UpdateTrainingSessionEndpointClient,
    UpdateTrainingSessionRequest,
    TrainingProgram,
    TrainingSessionDTO,
    TrainingSessionStatus
  } from '$lib/api/ApiClient';

  const slug = $page.params.slug;
  let program: TrainingProgram | undefined;
  let sessions: TrainingSessionDTO[] = [];
  let programID = '';
  let showModal = false;
  let showCompleted = true;

  onMount(async () => {
    const getProgramsClient = new GetTrainingProgramsEndpointClient('http://localhost:5174');

    try {
      const allPrograms = await getProgramsClient.getAll2();
      program = allPrograms.find(p => slugify(p.title ?? '') === slug);
      if (program) {
        programID = program.trainingProgramID!;
        sessions = program.trainingSessions ?? [];
        sessions.sort((a, b) => new Date(a.date ?? '').getTime() - new Date(b.date ?? '').getTime());
      }
    } catch (error) {
      console.error('Failed to load program', error);
    }
  });

  function formatDate(dateInput: Date | string | undefined): string {
    if (!dateInput) return '—';
    const date = typeof dateInput === 'string' ? new Date(dateInput) : dateInput;
    return date.toLocaleDateString(undefined, {
      weekday: 'short',
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }

  function getSessionPreview(session: TrainingSessionDTO): string[] {
    return session.movements?.slice(0, 3).map(m => {
      const s = m.sets && m.sets.length > 0 ? m.sets[0] : undefined;
      const rep = s?.recommendedReps ? `${s.recommendedReps} reps` : '';
      const rpe = s?.recommendedRPE ? `RPE ${s.recommendedRPE}` : '';
      const wt = s?.recommendedWeight ? `${s.recommendedWeight} ${s.weightUnit === 0 ? 'kg' : 'lbs'}` : '';
      return [m.movementBase?.name ?? '', rep, wt, rpe].filter(Boolean).join(' ');
    }) ?? [];
  }

  function getConsiderations(index: number): string[] {
    return ['Overshot last session', 'Recent poor sleep', 'Shoulder pain'].slice(0, (index % 3) + 1);
  }

  function safeDate(input: string | Date | undefined): Date {
    return input instanceof Date ? input : new Date(input ?? new Date());
  }

  async function toggleSkipSession(sessionID: string) {
    if (!program) return;
    const session = sessions.find(s => s.trainingSessionID === sessionID);
    if (!session) return;

    const newStatus = session.status === TrainingSessionStatus._3
      ? TrainingSessionStatus._0
      : TrainingSessionStatus._3;

    const updateSessionClient = new UpdateTrainingSessionEndpointClient('http://localhost:5174');

    try {
      await updateSessionClient.update4(
        UpdateTrainingSessionRequest.fromJS({
          trainingSessionID: sessionID,
          trainingProgramID: program.trainingProgramID!,
          date: new Date(session.date ?? new Date()),
          status: newStatus
        })
      );

      session.status = newStatus;
      sessions = [...sessions];
    } catch (err) {
      alert('Failed to update session status');
    }
  }

  function handleSessionCreated() {
    location.reload();
  }
</script>

{#if program}
  <div class="p-6 max-w-6xl mx-auto">
    <div class="flex justify-between items-center mb-6">
  <a href="/programs" class="inline-flex items-center text-sm text-white bg-zinc-700 hover:bg-zinc-600 px-3 py-1 rounded">
    ← Back to Library
  </a>
  <a href="/movementLib" class="inline-flex items-center text-sm text-white bg-blue-600 hover:bg-blue-500 px-3 py-1 rounded">
    🏋️ View Movement Library
  </a>
  </div>

    <h1 class="text-3xl font-bold mb-6">{program.title}</h1>

    <!-- Uncompleted Sessions -->
    <p class="text-green-400 font-semibold mb-4">Upcoming Sessions</p>
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 mb-10">
      {#each sessions.filter(s => s.status === TrainingSessionStatus._0 || s.status === undefined) as session (session.trainingSessionID)}
        <div class="relative bg-zinc-800 rounded-xl p-4 text-white shadow-md hover:shadow-lg hover:bg-zinc-700 transition">
          <a href={`/programs/${slug}/session/${session.trainingSessionID}`} class="block space-y-2">
            <h2 class="text-xl font-semibold">{formatDate(session.date)}</h2>
            <div class="grid grid-cols-2 gap-4">
              <div>
                <h3 class="font-bold text-sm mb-1">Preview</h3>
                <ul class="text-sm space-y-1">
                  {#each getSessionPreview(session) as item}
                    <li>- {item}</li>
                  {/each}
                </ul>
              </div>
              <div>
                <h3 class="font-bold text-sm mb-1">Considerations</h3>
                <ul class="text-sm space-y-1">
                  {#each getConsiderations(0) as point}
                    <li>- {point}</li>
                  {/each}
                </ul>
              </div>
            </div>
          </a>
          <button
            on:click={() => session.trainingSessionID && toggleSkipSession(session.trainingSessionID)}
            class="absolute top-2 right-2 bg-yellow-400 text-black px-2 py-1 rounded text-xs hover:bg-yellow-300"
            title="Skip this session"
          >
            ⏭ Skip
          </button>
        </div>
      {/each}
    </div>

    <!-- Skipped Sessions -->
    {#if sessions.some(s => s.status === TrainingSessionStatus._3)}
      <p class="text-yellow-300 font-semibold mb-4 mt-6">⏭️ Skipped Sessions</p>
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 opacity-50 mb-10">
        {#each sessions.filter(s => s.status === TrainingSessionStatus._3) as session (session.trainingSessionID)}
          <div class="bg-zinc-700 rounded-xl p-4 text-white shadow relative">
            <h2 class="text-xl font-semibold mb-2">Skipped – {formatDate(session.date)}</h2>
            <p class="text-sm italic text-gray-300 mb-2">This session was skipped.</p>
            <button
              on:click={() => session.trainingSessionID && toggleSkipSession(session.trainingSessionID)}
              class="text-xs text-blue-300 hover:underline"
            >
              Undo Skip
            </button>
          </div>
        {/each}
      </div>
    {/if}

    <!-- Completed Sessions -->
    {#if sessions.some(s => s.status === TrainingSessionStatus._2)}
      <div class="flex items-center justify-between mb-4 mt-6">
        <p class="text-blue-400 font-semibold">✅ Completed Sessions</p>
        <button
          on:click={() => showCompleted = !showCompleted}
          class="text-sm text-gray-300 underline hover:text-white"
        >
          {showCompleted ? 'Hide' : 'Show'}
        </button>
      </div>

      {#if showCompleted}
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 opacity-60">
          {#each sessions.filter(s => s.status === TrainingSessionStatus._2) as session (session.trainingSessionID)}
            <a href={`/programs/${slug}/session/${session.trainingSessionID}`} class="block">
              <div class="bg-zinc-700 rounded-xl p-4 text-white shadow hover:bg-zinc-600 transition">
                <h2 class="text-xl font-semibold mb-2">{formatDate(session.date)}</h2>
                <div class="grid grid-cols-2 gap-4">
                  <div>
                    <h3 class="font-bold text-sm mb-1">Preview</h3>
                    <ul class="text-sm space-y-1">
                      {#each getSessionPreview(session) as item}
                        <li>- {item}</li>
                      {/each}
                    </ul>
                  </div>
                  <div>
                    <h3 class="font-bold text-sm mb-1">Considerations</h3>
                    <ul class="text-sm space-y-1">
                      {#each getConsiderations(0) as point}
                        <li>- {point}</li>
                      {/each}
                    </ul>
                  </div>
                </div>
              </div>
            </a>
          {/each}
        </div>
      {/if}
    {/if}
  </div>
{:else}
  <div class="p-6 max-w-4xl mx-auto">
    <h1 class="text-3xl font-bold mb-4 text-red-400">Program not found</h1>
  </div>
{/if}

<!-- Add Session Floating Button -->
<button
  on:click={() => showModal = true}
  class="fixed bottom-6 right-6 bg-green-500 hover:bg-green-400 text-black rounded-full w-12 h-12 text-2xl shadow-lg z-40"
>
  +
</button>

<CreateSessionModal
  show={showModal}
  programID={programID}
  on:close={() => showModal = false}
  on:created={handleSessionCreated}
/>
