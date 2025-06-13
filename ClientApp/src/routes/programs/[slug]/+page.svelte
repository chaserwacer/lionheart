<script lang="ts">
  import { page } from '$app/stores';
  import { onMount } from 'svelte';
  import { programStorage } from '$lib/stores/programStore';
  import type { Program, TrainingSession } from '$lib/types/programs';
  import CreateSessionModal from '$lib/components/CreateSession.svelte';
  import { slugify } from '$lib/utils/slugify';

  const slug = $page.params.slug;
  let program: Program | undefined;
  let sessions: TrainingSession[] = [];
  let programID = '';
  let showModal = false;
  let showCompleted = true;

  onMount(() => {
    const allPrograms = programStorage.load();
    program = allPrograms.find(p => slugify(p.title) === slug);
    if (program) {
      programID = program.programID;
      sessions = [...program.trainingSessions].sort(
        (a, b) => new Date(a.date).getTime() - new Date(b.date).getTime()
      );
    }
  });

  function formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString(undefined, {
      weekday: 'short', year: 'numeric', month: 'short', day: 'numeric'
    });
  }

  const getSessionPreview = (session: TrainingSession): string[] =>
    session.movements.slice(0, 3).map(m => {
      const s = m.sets[0];
      const rep = s?.recommendedReps ? `${s.recommendedReps} reps` : '';
      const rpe = s?.recommendedRPE ? `RPE ${s.recommendedRPE}` : '';
      const wt = s?.recommendedWeight ? `${s.recommendedWeight} ${s.weightUnit}` : '';
      return [m.movementBase.name, rep, wt, rpe].filter(Boolean).join(' ');
    });

  function getConsiderations(i: number): string[] {
    return ['Overshot last session', 'Recent poor sleep', 'Shoulder pain'].slice(0, (i % 3) + 1);
  }

  function handleSessionCreated(event: CustomEvent<TrainingSession>) {
    if (!program) return;
    const newSession = event.detail;
    program.trainingSessions.push(newSession);
    programStorage.update(program);
    sessions = [...program.trainingSessions].sort(
      (a, b) => new Date(a.date).getTime() - new Date(b.date).getTime()
    );
  }

  function toggleSkipSession(sessionID: string) {
  if (!program) return;
  const index = program.trainingSessions.findIndex(s => s.sessionID === sessionID);
  if (index !== -1) {
    const current = program.trainingSessions[index];
    current.status = current.status === 'Skipped' ? 'Planned' : 'Skipped';
    programStorage.update(program);
    sessions = [...program.trainingSessions];
  }
}

</script>

{#if program}
  <div class="p-6 max-w-6xl mx-auto">
    <a href="/programs" class="inline-flex items-center mb-6 text-sm text-white bg-zinc-700 hover:bg-zinc-600 px-3 py-1 rounded">
      ← Back to Library
    </a>

    <h1 class="text-3xl font-bold mb-6">{program.title}</h1>

    <!-- Uncompleted Sessions -->
    <p class="text-green-400 font-semibold mb-4">Upcoming Sessions</p>
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 mb-10">
      {#each sessions.filter(s => s.status === 'Planned' || !s.status) as session (session.sessionID)}
        <div class="relative bg-zinc-800 rounded-xl p-4 text-white shadow-md hover:shadow-lg hover:bg-zinc-700 transition">
          <a href={`/programs/${slug}/session/${session.sessionID}`} class="block space-y-2">
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
            on:click={() => toggleSkipSession(session.sessionID)}
            class="absolute top-2 right-2 bg-yellow-400 text-black px-2 py-1 rounded text-xs hover:bg-yellow-300"
            title="Skip this session"
          >
            ⏭ Skip
          </button>
        </div>
      {/each}
    </div>

    <!-- Skipped Sessions -->
    {#if sessions.some(s => s.status === 'Skipped')}
      <p class="text-yellow-300 font-semibold mb-4 mt-6">⏭️ Skipped Sessions</p>
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 opacity-50 mb-10">
        {#each sessions.filter(s => s.status === 'Skipped') as session (session.sessionID)}
          <div class="bg-zinc-700 rounded-xl p-4 text-white shadow relative">
            <h2 class="text-xl font-semibold mb-2">Skipped – {formatDate(session.date)}</h2>
            <p class="text-sm italic text-gray-300 mb-2">This session was skipped.</p>
            <button
              on:click={() => toggleSkipSession(session.sessionID)}
              class="text-xs text-blue-300 hover:underline"
            >
              Undo Skip
            </button>
          </div>
        {/each}
      </div>
    {/if}


    <!-- Completed Sessions -->
    {#if sessions.some(s => s.status === 'Completed')}
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
          {#each sessions.filter(s => s.status === 'Completed') as session (session.sessionID)}
            <a href={`/programs/${slug}/session/${session.sessionID}`} class="block">
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
