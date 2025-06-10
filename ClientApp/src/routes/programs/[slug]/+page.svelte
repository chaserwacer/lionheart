<script lang="ts">
  import { fakePrograms } from '$lib/data/programs';
  import type { TrainingProgram, Session, Movement } from '$lib/data/programs';
  import { page } from '$app/stores';

  const slug = $page.params.slug;
  const program: TrainingProgram | undefined = fakePrograms.find(
    p => p.name.toLowerCase().replace(/\s+/g, '-') === slug
  );

  const sessions: Session[] = program
    ? program.blocks?.[0]?.sessions ?? []
    : [];

  const getSessionPreview = (session: Session): string[] => {
    return session.movements.slice(0, 3).map(movement => {
      const set = movement.sets[0];
      const rpeText = set.recommendedRpe ? `RPE ${set.recommendedRpe}` : '';
      const repText = set.recommendedReps ? `${set.recommendedReps} reps` : '';
      const weightText = set.recommendedWeight ? `${set.recommendedWeight} lbs` : '';
      return [movement.name, repText, weightText, rpeText].filter(Boolean).join(' ');
    });
  };

  const getConsiderations = (sessionNumber: number): string[] => {
    const suggestions = [
      ['Overshot last session', 'Recent poor sleep', 'Shoulder pain'],
      ['Shoulder pain', '', ''],
      ['Shoulder pain', 'Undershot last squat session', '']
    ];
    return suggestions[sessionNumber - 1] || [];
  };
</script>

{#if program}
  <div class="p-6 max-w-6xl mx-auto">
    <h1 class="text-3xl font-bold mb-4">{program.name}</h1>
    <p class="text-green-400 font-semibold mb-4">Uncompleted Sessions ↓</p>

    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
      {#each sessions as session, i}
        <a href={`/programs/${slug}/session/${session.sessionNumber}`} class="block">
          <div class="bg-zinc-800 rounded-xl p-4 text-white shadow-md hover:shadow-lg hover:bg-zinc-700 transition">
            <h2 class="text-xl font-semibold mb-2">
              Session {session.sessionNumber} - {session.date}
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
                  {#each getConsiderations(session.sessionNumber) as point}
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
