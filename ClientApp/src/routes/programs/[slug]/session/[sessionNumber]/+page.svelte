<script lang="ts">
  import { page } from '$app/stores';
  import { fakePrograms } from '$lib/testData/programs';
  import type { Movement, Session, SetEntry } from '$lib/testData/programs';
  import { onMount } from 'svelte';

  const slug = $page.params.slug;
  const sessionNumber = parseInt($page.params.sessionNumber);

  const program = fakePrograms.find(p => p.name.toLowerCase().replace(/\s+/g, '-') === slug);
  const session: Session | undefined = program?.sessions
    ?.find(s => s.sessionNumber === sessionNumber);

  let movements: Movement[] = session?.movements || [];
  let completedIndices = new Set<number>();
  let unitMap: Record<number, 'lbs' | 'kg'> = {};
  let weightStep: number = 5;

  const allowedSteps = [1, 5, 10, 25];

  onMount(() => {
    movements = movements.map((movement) => ({
      ...movement,
      sets: movement.sets.map(set => ({
        ...set,
        recommendedRpe: typeof set.recommendedRpe === 'number' ? set.recommendedRpe : 7,
        actualRpe: typeof set.actualRpe === 'number' ? set.actualRpe : (typeof set.recommendedRpe === 'number' ? set.recommendedRpe : 7),
        actualReps: typeof set.actualReps === 'number' ? set.actualReps : set.recommendedReps,
        actualWeight: typeof set.actualWeight === 'number' ? set.actualWeight : set.recommendedWeight,
      }))
    }));
    movements.forEach((_, index) => unitMap[index] = 'lbs');
  });

  function getRpeColor(actual: number | undefined, target: number | undefined) {
    if (typeof actual !== 'number' || typeof target !== 'number') return 'bg-zinc-900';
    const diff = actual - target;
    if (diff >= 1) return 'bg-red-600';
    if (diff <= -1) return 'bg-blue-600';
    return 'bg-green-600';
  }

  function toKg(lbs: number): number {
    return Math.round(lbs * 0.453592);
  }

  function resetSet(mvIndex: number, setIndex: number) {
    movements = movements.map((movement, i) => {
      if (i !== mvIndex) return movement;
      const updatedSets = movement.sets.map((set, j) => {
        if (j !== setIndex) return set;
        return {
          ...set,
          actualWeight: set.recommendedWeight,
          actualReps: set.recommendedReps,
          actualRpe: set.recommendedRpe
        };
      });
      return { ...movement, sets: updatedSets };
    });
  }

  function toggleComplete(index: number) {
    const updated = new Set(completedIndices);
    if (updated.has(index)) {
      updated.delete(index);
    } else {
      updated.add(index);
    }
    completedIndices = updated;
  }
</script>

{#if session}
  <div class="p-6 max-w-6xl mx-auto">
    <h1 class="text-4xl font-bold mb-6">Session {session.sessionNumber} - {program?.name}</h1>

    <div class="mb-4">
      <label class="text-white text-sm mr-2">Weight step increment:</label>
      <select bind:value={weightStep} class="p-1 rounded bg-zinc-900 text-white">
        {#each allowedSteps as step}
          <option value={step}>{step}</option>
        {/each}
      </select>
    </div>

    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 items-start">
      {#each movements as movement, mvIndex (movement.name)}
        {#if !completedIndices.has(mvIndex)}
          <div class="bg-zinc-800 text-white rounded-xl p-4 shadow-md w-full mx-auto max-w-sm self-start">
            <div class="flex justify-between items-start mb-2">
              <div>
                <h2 class="text-2xl font-bold">{movement.name}</h2>
                <p class="text-sm text-gray-400 italic">{movement.type ?? 'Unknown Type'}</p>
              </div>
              <div class="text-sm text-gray-300 italic w-32 text-right">💡 Lorem ipsum dolor sit amet.</div>
            </div>

            <div class="flex justify-between items-center mb-2 text-sm">
              <label class="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={unitMap[mvIndex] === 'kg'}
                  on:change={() => unitMap[mvIndex] = unitMap[mvIndex] === 'lbs' ? 'kg' : 'lbs'} />
                Use kg
              </label>
              <button on:click={() => toggleComplete(mvIndex)} class="text-xs text-green-400 hover:underline">Mark Complete</button>
            </div>

            <ul class="space-y-2">
              {#each movement.sets as set, setIndex}
                <li class={`bg-zinc-700 p-3 rounded ${setIndex === 0 ? 'border-l-4 border-yellow-400' : ''}`}>
                  <div class="flex flex-wrap gap-3 justify-between items-start text-sm">
                    <div class="flex flex-col gap-1">
                      <span class="text-gray-300">Reps:</span>
                      <input
                        type="number"
                        class="bg-zinc-900 text-white p-1 rounded w-14 text-center"
                        step="1"
                        bind:value={set.actualReps} />
                    </div>
                    <div class="flex flex-col gap-1">
                      <span class="text-gray-300">RPE:</span>
                      <input
                        type="number"
                        step="0.5"
                        class={`text-white p-1 rounded w-14 text-center ${getRpeColor(set.actualRpe, set.recommendedRpe)}`}
                        bind:value={set.actualRpe}
                        on:input={() => movements = [...movements]}
                      />
                    </div>
                    <div class="flex flex-col gap-1 items-center">
                      <span class="text-gray-300">Weight:</span>
                      <input
                        type="number"
                        class="bg-zinc-900 text-white p-1 rounded w-20 text-center"
                        step={weightStep}
                        bind:value={set.actualWeight} />
                      <span class="text-xs text-gray-400">Recommended: {set.recommendedWeight} {unitMap[mvIndex]}</span>
                    </div>
                    <button on:click={() => resetSet(mvIndex, setIndex)} class="text-xs text-red-400 hover:underline mt-1">Reset</button>
                  </div>
                </li>
              {/each}
            </ul>

            <div class="mt-2">
              <label class="text-xs text-gray-300">Notes:</label>
              <textarea class="w-full mt-1 bg-zinc-900 text-white p-2 rounded text-sm resize-none" rows="2" bind:value={movement.notes}></textarea>
            </div>
          </div>
        {/if}
      {/each}
    </div>

    {#if Array.from(completedIndices).length}
      <h2 class="text-2xl text-white font-semibold mt-10 mb-4">✅ Completed Exercises</h2>
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 items-start">
        {#each movements as movement, mvIndex (movement.name)}
          {#if completedIndices.has(mvIndex)}
            <div class="bg-zinc-700 text-white rounded-xl p-4 shadow w-full mx-auto max-w-sm opacity-60">
              <h3 class="text-xl font-bold">{movement.name}</h3>
              <p class="text-sm italic text-gray-300">Marked complete</p>
              <button on:click={() => toggleComplete(mvIndex)} class="text-xs text-yellow-300 hover:underline mt-2">Undo</button>
            </div>
          {/if}
        {/each}
      </div>
    {/if}
  </div>
{:else}
  <div class="p-6 max-w-4xl mx-auto text-red-400">
    <h1 class="text-2xl font-bold">Session not found</h1>
  </div>
{/if}
