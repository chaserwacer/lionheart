<script lang="ts">
  import { onMount } from 'svelte';
  import { slugify } from '$lib/utils/slugify';
  import { goto } from '$app/navigation';
  import CreateProgramModal from '$lib/components/CreateProgram.svelte';

  import {
    TrainingProgram,
    TrainingSessionStatus,
    WeightUnit,
    GetTrainingProgramsEndpointClient,
    DeleteTrainingProgramEndpointClient
  } from '$lib/api/ApiClient';

  let showModal = false;
  let programs: TrainingProgram[] = [];
  const baseUrl = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5174';

  async function loadPrograms() {
    const getProgramsClient = new GetTrainingProgramsEndpointClient(baseUrl);
    try {
      programs = await getProgramsClient.getAll3();
    } catch (error) {
      console.error('Failed to load programs:', error);
    }
  }

  onMount(loadPrograms);

  async function handleProgramCreated() {
    await loadPrograms();
    showModal = false;
  }

  async function deleteProgram(programID: string) {
    const confirmed = confirm('Are you sure you want to delete this program?');
    if (!confirmed) return;

    const deleteProgramClient = new DeleteTrainingProgramEndpointClient(baseUrl);
    try {
      await deleteProgramClient.delete3(programID);
      programs = programs.filter(p => p.trainingProgramID !== programID);
    } catch (err) {
      alert('Failed to delete program.');
    }
  }

  function goToProgram(program: TrainingProgram) {
    goto(`/programs/${slugify(program.title ?? '')}`);
  }

  function formatDate(dateInput: Date | string | undefined): string {
    if (!dateInput) return '—';
    const date = typeof dateInput === 'string' ? new Date(dateInput) : dateInput;
    const options = { month: 'short', day: 'numeric' } as const;
    return date.toLocaleDateString(undefined, options);
  }

  function getTypeColor(type: string) {
    switch (type) {
      case "Powerlifting": return "bg-red-500";
      case "Bodybuilding": return "bg-pink-500";
      case "General Fitness": return "bg-blue-500";
      case "Running": return "bg-green-500";
      case "Biking": return "bg-yellow-400";
      case "Swimming": return "bg-cyan-500";
      default: return "bg-gray-500";
    }
  }

  function getNextWorkoutHighlights(program: TrainingProgram) {
    const firstSession = program.trainingSessions?.[0];
    return firstSession?.movements?.slice(0, 3).map(movement => {
      const set = movement.sets?.[0];
      const rep = set?.recommendedReps ? `${set.recommendedReps} reps` : '';
      const rpe = set?.recommendedRPE ? `RPE ${set.recommendedRPE}` : '';
      const weight = set?.recommendedWeight
        ? `${set.recommendedWeight} ${set.weightUnit === WeightUnit._0 ? 'kg' : 'lbs'}`
        : '';
      return [movement.movementBase?.name ?? '', rep, weight, rpe].filter(Boolean).join(' ');
    }) ?? [];
  }

  function calculateProgress(program: TrainingProgram): number {
    const sessions = program.trainingSessions ?? [];
    const completed = sessions.filter(s => s.status === TrainingSessionStatus._2).length;
    return sessions.length === 0 ? 0 : Math.round((completed / sessions.length) * 100);
  }
</script>


<div class="p-6 max-w-6xl mx-auto">
  <h1 class="text-3xl font-bold mb-6">Blake's Program Library</h1>

  <div class="grid gap-6 md:grid-cols-2">
    {#each programs as program (program.trainingProgramID)}
      <div class="relative group">
        <div
          role="link"
          tabindex="0"
          class="block cursor-pointer"
          on:click={() => goToProgram(program)}
          on:keydown={(e) => e.key === 'Enter' && goToProgram(program)}
        >
          <div class="bg-zinc-900 rounded-xl p-6 shadow-lg hover:shadow-2xl transition hover:bg-zinc-800 text-white h-[320px] flex flex-col justify-between">
            <div class="flex items-center justify-between mb-4">
              <h2 class="font-bold truncate text-2xl md:text-3xl w-2/3">{program.title ?? 'Untitled'}</h2>
              <div class="flex items-center gap-2">
                <span class={`text-xs font-semibold text-black px-3 py-1 rounded ${getTypeColor(program.tags?.[0] ?? '')}`}>
                  {program.tags?.[0] ?? 'Unknown'}
                </span>
                <button
                  type="button"
                  on:click|stopPropagation={() => {
                    if (program.trainingProgramID) {
                      deleteProgram(program.trainingProgramID);
                    }
                  }}
                  class="text-red-400 hover:text-red-600 text-xl font-bold"
                  title="Delete program"
                >
                  &times;
                </button>
              </div>
            </div>

            <div class="flex flex-col md:flex-row md:justify-between md:items-start gap-4">
              <div class="flex-1">
                <p class="text-base text-gray-300">
                  <span class="font-medium">Start:</span> {formatDate(program.startDate)}
                </p>
                <p class="text-base text-gray-300">
                  <span class="font-medium">End:</span> {formatDate(program.endDate)}
                </p>
              </div>

              <div class="flex-1">
                <h3 class="text-base text-gray-200 font-semibold mb-1">Next Workout Highlights:</h3>
                <ul class="text-base text-gray-100 space-y-1">
                  {#each getNextWorkoutHighlights(program) as line}
                    <li class="border-l-4 border-zinc-700 pl-3">{line}</li>
                  {/each}
                </ul>
              </div>
            </div>

            <div>
              <p class="text-sm text-gray-400 mt-4 mb-2">
                <span class="font-medium">Next Workout:</span> {formatDate(program.nextTrainingSessionDate)}
              </p>
              <div class="relative w-full bg-zinc-700 h-5 rounded-full overflow-hidden">
                <div
                  class={`h-5 rounded-full absolute left-0 top-0 ${getTypeColor(program.tags?.[0] ?? '')}`}
                  style={`width: ${calculateProgress(program)}%`}
                ></div>
                <div class="absolute right-2 top-0 h-5 text-xs text-white font-bold flex items-center">
                  {calculateProgress(program)}%
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    {/each}
  </div>
</div>

<button
  on:click={() => showModal = true}
  class="fixed bottom-6 right-6 bg-green-500 hover:bg-green-400 text-black rounded-full w-12 h-12 text-2xl shadow-lg z-40"
>
  +
</button>

<CreateProgramModal
  show={showModal}
  on:close={() => showModal = false}
  on:created={handleProgramCreated}
/>