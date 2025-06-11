<script lang="ts">
  import { fakePrograms } from '$lib/testData/programs';
  import type { Program, TrainingSession, Movement } from '$lib/types/programs';
  import { page } from '$app/stores';
    import { onMount } from 'svelte';
  import { programStorage } from '$lib/stores/programStore';
  import { v4 as uuid } from 'uuid';

  const slug = $page.params.slug;
  let program: Program | undefined = fakePrograms.find(
    p => p.title.toLowerCase().replace(/\s+/g, '-') === slug
  );
  
  const sessions: TrainingSession[] = program
    ? program.trainingSessions ?? []
    : [];

  const getSessionPreview = (session: TrainingSession): string[] => {
    return session.movements.slice(0, 3).map(movement => {
      const set = movement.sets[0];
      const rpeText = set.recommendedRPE ? `RPE ${set.recommendedRPE}` : '';
      const repText = set.recommendedReps ? `${set.recommendedReps} reps` : '';
      const weightText = set.recommendedWeight ? `${set.recommendedWeight} ${set.weightUnit}` : '';
      return [movement.movementBase.name, repText, weightText, rpeText].filter(Boolean).join(' ');
    });
  };

  const getConsiderations = (sessionIndex: number): string[] => {
    const suggestions = [
      ['Overshot last session', 'Recent poor sleep', 'Shoulder pain'],
      ['Shoulder pain', '', ''],
      ['Shoulder pain', 'Undershot last squat session', '']
    ];
    return suggestions[sessionIndex] || [];
  };

  let showModal = false;
  let newSessionNote = '';

  onMount(() => {
    const all = programStorage.load();
    const real = all.find(p => p.title.toLowerCase().replace(/\s+/g, '-') === slug);
    if (real) {
      program = real;
      sessions.length = 0;
      sessions.push(...real.trainingSessions);
    }
  });

  function addSession() {
    if (!program || !newSessionNote) return;

    const newSession: TrainingSession = {
      sessionID: uuid(),
      programID: program.programID,
      date: new Date().toISOString(),
      movements: [],
    };

    program.trainingSessions.push(newSession);
    programStorage.update(program);
    sessions.push(newSession);
    newSessionNote = '';
    showModal = false;
  }
</script>

{#if program}
  <div class="p-6 max-w-6xl mx-auto">
    <h1 class="text-3xl font-bold mb-4">{program.title}</h1>
    <p class="text-green-400 font-semibold mb-4">Uncompleted Sessions ↓</p>

    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
      {#each sessions as session, i}
        <a href={`/programs/${slug}/session/${i + 1}`} class="block">
          <div class="bg-zinc-800 rounded-xl p-4 text-white shadow-md hover:shadow-lg hover:bg-zinc-700 transition">
            <h2 class="text-xl font-semibold mb-2">
              Session {i + 1} - {session.date}
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
                  {#each getConsiderations(i) as point}
                    <li>- {point || '__________'}</li>
                  {/each}
                </ul>
              </div>
            </div>
          </div>
        </a>
      {/each}
    </div>
  </div>
{:else}
  <div class="p-6 max-w-4xl mx-auto">
    <h1 class="text-3xl font-bold mb-4 text-red-400">Program not found</h1>
  </div>
{/if}
{#if showModal}
  <div class="fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center">
    <div class="bg-white rounded-lg p-6 w-full max-w-md text-black">
      <h2 class="text-xl font-bold mb-4">Add New Session</h2>
      <input
        bind:value={newSessionNote}
        placeholder="Session notes or label"
        class="w-full px-3 py-2 border border-gray-300 rounded mb-4"
      />
      <div class="flex justify-end space-x-2">
        <button on:click={() => showModal = false} class="px-4 py-2 bg-gray-300 rounded hover:bg-gray-400">Cancel</button>
        <button on:click={addSession} class="px-4 py-2 bg-green-500 text-white rounded hover:bg-green-600">Add</button>
      </div>
    </div>
  </div>
{/if}
<button
  on:click={() => showModal = true}
  class="fixed bottom-6 right-6 bg-green-500 hover:bg-green-400 text-black rounded-full w-12 h-12 text-2xl shadow-lg z-40"
>
  +
</button>
