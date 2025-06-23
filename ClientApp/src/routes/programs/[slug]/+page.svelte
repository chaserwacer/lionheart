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
    TrainingSessionStatus,
    DeleteTrainingSessionEndpointClient
  } from '$lib/api/ApiClient';

  const slug = $page.params.slug;
  let program: TrainingProgram | undefined;
  let sessions: TrainingSessionDTO[] = [];
  let programID = '';
  let showModal = false;
  let showCompleted = true;
  let showUpcoming = true;
  let showSkipped = true;
  const baseUrl = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5174';

  async function loadSessions() {
    const getProgramsClient = new GetTrainingProgramsEndpointClient(baseUrl);

    try {
      const allPrograms = await getProgramsClient.getAll3();
      program = allPrograms.find(p => slugify(p.title ?? '') === slug);
      if (program) {
        programID = program.trainingProgramID!;
        sessions = program.trainingSessions ?? [];
        sessions.sort((a, b) => new Date(a.date ?? '').getTime() - new Date(b.date ?? '').getTime());
      }
    } catch (error) {
      console.error('Failed to load program', error);
    }
  }

  onMount(async () => {
    await loadSessions();
    assignSessionNumbers();
  });

  function assignSessionNumbers() {
  sessions = [...sessions]
    .sort((a, b) => new Date(a.date ?? '').getTime() - new Date(b.date ?? '').getTime())
    .map((s, i) => {
      const updated = TrainingSessionDTO.fromJS(s); // preserve DTO methods
      updated.sessionNumber = i + 1;
      return updated;
    });
}

  
  async function deleteSession(sessionID: string) {
    const confirmed = confirm("Are you sure you want to delete this session?");
    if (!confirmed || !program) return;

    const deleteClient = new DeleteTrainingSessionEndpointClient(baseUrl);
    try {
      await deleteClient.delete4(sessionID);
      sessions = sessions.filter(s => s.trainingSessionID !== sessionID);
      assignSessionNumbers();
    } catch {
      alert("Failed to delete session.");
    }
  }

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

    const updateClient = new UpdateTrainingSessionEndpointClient(baseUrl);
    try {
      await updateClient.update4(
        UpdateTrainingSessionRequest.fromJS({
          trainingSessionID: sessionID,
          trainingProgramID: program.trainingProgramID!,
          date: new Date(session.date ?? new Date()),
          status: newStatus
        })
      );
      session.status = newStatus;
      assignSessionNumbers();
    } catch {
      alert('Failed to update session status');
    }
  }

  async function handleSessionCreated() {
    await loadSessions();
    assignSessionNumbers();
    showModal = false;
  }

 async function shiftSessionDate(session: TrainingSessionDTO, deltaDays: number) {
  if (!program) return;

  const newDate = new Date(session.date ?? new Date());
  newDate.setDate(newDate.getDate() + deltaDays);

  // Update the local session object
  session.date = newDate;

  // Save to backend
  const updateClient = new UpdateTrainingSessionEndpointClient(baseUrl);
  try {
    await updateClient.update4(UpdateTrainingSessionRequest.fromJS({
      trainingSessionID: session.trainingSessionID!,
      trainingProgramID: program.trainingProgramID!,
      date: newDate,
      status: session.status ?? TrainingSessionStatus._0
    }));

    // Trigger Svelte update
    sessions = [...sessions];
    assignSessionNumbers(); // Re-sort based on new dates

  } catch (err) {
    console.error('Failed to update session date', err);
    alert('Could not update session date.');
  }
}
</script>
{#if program}
  <div class="p-6 max-w-6xl mx-auto text-base-content">
    <div class="flex justify-between items-center mb-6">
      <a href="/programs" class="btn btn-sm btn-primary">
        ← Program Library
      </a>
      <a href="/movementLib" class="btn btn-sm btn-primary">
        Movement Library →
      </a>
    </div>

    <h1 class="text-4xl font-extrabold mb-6">{program.title}</h1>

    <div class="mb-4 flex items-center justify-between">
      <h2 class="text-2xl font-bold flex items-center gap-2">
        Upcoming Sessions
        <button on:click={() => showUpcoming = !showUpcoming} class="btn btn-xs btn-outline btn-primary">
          {showUpcoming ? '🡫' : '🡪'}
        </button>
      </h2>
    </div>

    {#if showUpcoming}
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 mb-10">
        {#each sessions.filter(s => s.status === TrainingSessionStatus._0 || s.status === undefined) as session (session.trainingSessionID)}
          <div class="bg-base-100 text-base-content border border-base-300 rounded-xl shadow-md hover:shadow-lg transition p-4">
            <div class="flex items-center justify-between border-b border-base-300 pb-2 mb-3">
              <span class="bg-primary text-primary-content text-xs px-2 py-1 rounded font-mono">
                # {session.sessionNumber}
              </span>
              <div class="flex items-center gap-2">
                <button on:click={() => shiftSessionDate(session, -1)} class="btn btn-xs btn-outline btn-primary">←</button>
                <h2 class="text-sm md:text-base font-semibold">{formatDate(session.date)}</h2>
                <button on:click={() => shiftSessionDate(session, 1)} class="btn btn-xs btn-outline btn-primary">→</button>
              </div>
            </div>

            <a href={`/programs/${slug}/session/${session.trainingSessionID}`} class="space-y-4 mt-3 block">
              <div class="bg-base-200 p-3 rounded-lg border border-base-300">
                <h3 class="font-semibold text-sm mb-1 text-base-content/80">Preview</h3>
                <ul class="text-sm space-y-1">
                  {#each getSessionPreview(session) as item}
                    <li>- {item}</li>
                  {/each}
                </ul>
              </div>

              <div class="bg-base-200 p-3 rounded-lg border border-base-300">
                <h3 class="font-semibold text-sm mb-1 text-base-content/80">Considerations</h3>
                <ul class="text-sm space-y-1">
                  {#each getConsiderations(0) as point}
                    <li>- {point}</li>
                  {/each}
                </ul>
              </div>
            </a>

            <div class="flex justify-end gap-2 mt-4">
              <button on:click={() => session.trainingSessionID && toggleSkipSession(session.trainingSessionID)}
                class={`btn btn-xs ${session.status === TrainingSessionStatus._3 ? 'btn-warning' : 'btn-outline btn-primary'}`}>
                {session.status === TrainingSessionStatus._3 ? 'Undo Skip' : 'Skip'}
              </button>
              <button on:click={() => session.trainingSessionID && deleteSession(session.trainingSessionID)}
                class="btn btn-xs btn-error text-white">
                Delete
              </button>
            </div>
          </div>
        {/each}
      </div>
    {/if}

    {#if sessions.some(s => s.status === TrainingSessionStatus._3)}
      <div class="mb-4 flex items-center justify-between mt-6">
        <h2 class="text-xl font-bold flex items-center gap-2">
          Skipped Sessions
          <button on:click={() => showSkipped = !showSkipped} class="btn btn-xs btn-outline btn-primary">
            {showSkipped ? '🡫' : '🡪'}
          </button>
        </h2>
      </div>

      {#if showSkipped}
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 opacity-60 mb-10">
          {#each sessions.filter(s => s.status === TrainingSessionStatus._3) as session (session.trainingSessionID)}
            <div class="bg-base-100 text-base-content border border-base-300 rounded-xl p-4 shadow">
              <h2 class="text-lg font-semibold mb-2">Skipped – {formatDate(session.date)}</h2>
              <p class="text-sm italic text-base-content mb-2">This session was skipped.</p>
              <button
                on:click={() => session.trainingSessionID && toggleSkipSession(session.trainingSessionID)}
                class="text-xs btn btn-link text-primary"
              >
                Undo Skip
              </button>
            </div>
          {/each}
        </div>
      {/if}
    {/if}

    {#if sessions.some(s => s.status === TrainingSessionStatus._2)}
      <div class="mb-4 flex items-center justify-between mt-6">
        <h2 class="text-xl font-bold flex items-center gap-2">
          Completed Sessions
          <button on:click={() => showCompleted = !showCompleted} class="btn btn-xs btn-outline btn-primary">
            {showCompleted ? '🡫' : '🡪'}
          </button>
        </h2>
      </div>

      {#if showCompleted}
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 opacity-60">
          {#each sessions.filter(s => s.status === TrainingSessionStatus._2) as session (session.trainingSessionID)}
            <a href={`/programs/${slug}/session/${session.trainingSessionID}`} class="block">
              <div class="bg-base-100 text-base-content border border-base-300 rounded-xl p-4 shadow hover:shadow-md transition">
                <h2 class="text-lg font-semibold mb-2">{formatDate(session.date)}</h2>
                <div class="grid grid-cols-2 gap-4">
                  <div>
                    <h3 class="font-semibold text-sm mb-1">Preview</h3>
                    <ul class="text-sm space-y-1">
                      {#each getSessionPreview(session) as item}
                        <li>- {item}</li>
                      {/each}
                    </ul>
                  </div>
                  <div>
                    <h3 class="font-semibold text-sm mb-1">Considerations</h3>
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
    <h1 class="text-3xl font-bold mb-4 text-error">Program not found</h1>
  </div>
{/if}

<!-- Floating Add Button -->
<button
  on:click={() => showModal = true}
  class="fixed bottom-6 right-6 btn btn-circle btn-primary text-xl shadow-lg z-40"
>
  +
</button>


<CreateSessionModal
  show={showModal}
  programID={programID}
  existingSessionCount={sessions.length}
  on:close={() => showModal = false}
  on:createdWithSession={() => {
    showModal = false;
    setTimeout(() => location.reload(), 50);
  }}
/>


