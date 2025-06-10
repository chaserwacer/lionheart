<script lang="ts">
  import { fakePrograms } from '$lib/testData/programs';
  import type { TrainingProgram } from '$lib/testData/programs';

  function formatDate(dateStr: string) {
    const options = { month: 'short', day: 'numeric' } as const;
    return new Date(dateStr).toLocaleDateString(undefined, options);
  }

  function getTypeColor(type: string) {
    switch (type) {
      case "Powerlifting": return "bg-red-500";
      case "General Fitness": return "bg-blue-500";
      case "Swimming": return "bg-cyan-500";
      case "Biking": return "bg-yellow-400";
      default: return "bg-gray-500";
    }
  }

  function getNextWorkoutHighlights(program: TrainingProgram) {
    const firstSession = program.sessions[0];
    return firstSession?.movements.slice(0, 3).map(movement => {
      const set = movement.sets[0];
      const rpeText = set.recommendedRpe ? `RPE ${set.recommendedRpe}` : '';
      const repText = set.recommendedReps ? `${set.recommendedReps} reps` : '';
      const weightText = set.recommendedWeight ? `${set.recommendedWeight} lbs` : '';
      return [movement.name, repText, weightText, rpeText].filter(Boolean).join(' ');
    }) ?? [];
  }
</script>

<div class="p-6 max-w-6xl mx-auto">
  <h1 class="text-3xl font-bold mb-6">Blake's Program Library</h1>

  <div class="grid gap-6 md:grid-cols-2">
    {#each fakePrograms as program, i (program.name)}
      <a
        href={`/programs/${program.name.replace(/\s+/g, '-').toLowerCase()}`}
        class="block"
      >
        <div class="bg-zinc-900 rounded-xl p-6 shadow-lg hover:shadow-2xl transition cursor-pointer hover:bg-zinc-800 text-white h-[320px] flex flex-col justify-between">
          <!-- Header -->
          <div class="flex items-center justify-between mb-4">
            <h2 class="font-bold truncate text-2xl md:text-3xl w-2/3">{program.name}</h2>
            <span class={`text-xs font-semibold text-black px-3 py-1 rounded ${getTypeColor(program.type)}`}>{program.type}</span>
          </div>

          <!-- Info Row -->
          <div class="flex flex-col md:flex-row md:justify-between md:items-start gap-4">
            <!-- Date Info -->
            <div class="flex-1">
              <p class="text-base text-gray-300"><span class="font-medium">Start:</span> {formatDate(program.startDate)}</p>
              <p class="text-base text-gray-300"><span class="font-medium">End:</span> {formatDate(program.endDate)}</p>
            </div>

            <!-- Workout Highlights -->
            <div class="flex-1">
              <h3 class="text-base text-gray-200 font-semibold mb-1">Next Workout Highlights:</h3>
              <ul class="text-base text-gray-100 space-y-1">
                {#each getNextWorkoutHighlights(program) as line}
                  <li class="border-l-4 border-zinc-700 pl-3">{line}</li>
                {/each}
              </ul>
            </div>
          </div>

          <!-- Footer: Next Workout Date + Progress -->
          <div>
            <p class="text-sm text-gray-400 mt-4 mb-2"><span class="font-medium">Next Workout:</span> {formatDate(program.nextWorkoutDate)}</p>
            <div class="relative w-full bg-zinc-700 h-5 rounded-full">
              <div
                class="bg-green-500 h-5 rounded-full text-xs text-black font-bold flex items-center justify-center"
                style="width: 50%"
              >
                50%
              </div>
            </div>
          </div>
        </div>
      </a>
    {/each}
  </div>
</div>

<style>
  a:hover {
    text-decoration: none;
  }
</style>
