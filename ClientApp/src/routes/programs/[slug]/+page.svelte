<script lang="ts">
  import { page } from '$app/stores';
  import { onMount } from 'svelte';
  import { programStorage } from '$lib/stores/programStore';
  import type { Program, TrainingSession } from '$lib/types/programs';
  import { v4 as uuid } from 'uuid';
  import CreateSessionModal from '$lib/components/CreateSession.svelte';
  import { slugify } from '$lib/utils/slugify';

  const slug = $page.params.slug;
  let program: Program | undefined;
  let sessions: TrainingSession[] = [];
  let showModal = false;
  let programID = '';
  let showCompleted = true;

  onMount(() => {
    const allPrograms = programStorage.load();
    const match = allPrograms.find(p => slugify(p.title) === slug);
    if (match) {
      program = match;
      programID = match.programID;
      sessions = [...match.trainingSessions].sort(
        (a, b) => new Date(a.date).getTime() - new Date(b.date).getTime()
      );
    }
  });

  function formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString(undefined, {
      weekday: 'short',
      year: 'numeric',
      month: 'short',
      day: 'numeric'
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

  const getConsiderations = (i: number): string[] => [
    'Overshot last session',
    'Recent poor sleep',
    'Shoulder pain'
  ].slice(0, (i % 3) + 1);

  function handleSessionCreated(event: CustomEvent<TrainingSession>) {
    if (!program) return;
    const newSession = event.detail;
    program.trainingSessions.push(newSession);
    programStorage.update(program);
    sessions = [...program.trainingSessions].sort(
      (a, b) => new Date(a.date).getTime() - new Date(b.date).getTime()
    );
  }
</script>

{#if program}
  <div class="p-6 max-w-6xl mx-auto">
    <h1 class="text-3xl font-bold mb-4">{program.title}</h1>
    <p class="text-green-400 font-semibold mb-4">Uncompleted Sessions ↓</p>

    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 mb-12">
      {#each sessions.filter(s => s.status !== 'Completed') as session (session.sessionID)}
        <a href={`/programs/${slug}/session/${session.sessionID}`} class="block">
          <div class="bg-zinc-800 rounded-xl p-4 text-white shadow-md hover:shadow-lg hover:bg-zinc-700 transition">
            <h2 class="text-xl font-semibold mb-2">
              {formatDate(session.date)}
            </h2>
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
                    <li>- {point || '__________'}</li>
                  {/each}
                </ul>
              </div>
            </div>
          </div>
        </a>
      {/each}
    </div>

    {#if sessions.some(s => s.status === 'Completed')}
      <div class="flex items-center justify-between mb-4 mt-6">
        <p class="text-yellow-400 font-semibold">✅ Completed Sessions</p>
        <button on:click={() => showCompleted = !showCompleted} class="text-sm text-gray-300 underline">
          {showCompleted ? 'Hide' : 'Show'}
        </button>
      </div>

      {#if showCompleted}
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 opacity-60">
          {#each sessions.filter(s => s.status === 'Completed') as session (session.sessionID)}
            <a href={`/programs/${slug}/session/${session.sessionID}`} class="block">
              <div class="bg-zinc-700 rounded-xl p-4 text-white shadow hover:shadow-md hover:bg-zinc-600 transition">
                <h2 class="text-xl font-semibold mb-2">
                  {formatDate(session.date)}
                </h2>
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
                        <li>- {point || '__________'}</li>
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

<!-- Floating add session button -->
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
