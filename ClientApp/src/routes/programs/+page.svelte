<script lang="ts">
  import { onMount } from 'svelte';
  import { slugify } from '$lib/utils/slugify';
  import { goto } from '$app/navigation';
  import CreateProgramModal from '$lib/components/CreateProgram.svelte';

  import {
    TrainingProgramDTO,
    TrainingSessionStatus,
    WeightUnit,
    GetTrainingProgramsEndpointClient,
    DeleteTrainingProgramEndpointClient,
    HasCreatedProfileEndpointClient,

    TrainingProgram

  } from '$lib/api/ApiClient';

  let showModal = false;
  let programs: TrainingProgramDTO[] = [];
  let username = '';
  const baseUrl = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5174';

  onMount(async () => {
    await loadUser();
    await loadPrograms();
  });

  async function loadUser() {
    try {
      const client = new HasCreatedProfileEndpointClient(baseUrl);
      const user = await client.hasCreatedProfile();
      username = user.name ?? 'User';
    } catch (err) {
      console.error('Failed to fetch user profile:', err);
    }
  }

  async function loadPrograms() {
    try {
      const getProgramsClient = new GetTrainingProgramsEndpointClient(baseUrl);
      programs = await getProgramsClient.getAll3();
    } catch (error) {
      console.error('Failed to load programs:', error);
    }
  }

  async function handleProgramCreated() {
    await loadPrograms();
    showModal = false;
  }

  async function deleteProgram(programID: string) {
    const confirmed = confirm('Are you sure you want to delete this program?');
    if (!confirmed) return;

    try {
      const deleteProgramClient = new DeleteTrainingProgramEndpointClient(baseUrl);
      await deleteProgramClient.delete3(programID);
      programs = programs.filter(p => p.trainingProgramID !== programID);
    } catch (err) {
      alert('Failed to delete program.');
      console.error(err);
    }
  }

  function goToProgram(program: TrainingProgramDTO) {
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
      case "Powerlifting": return "bg-error text-error-content";
      case "Bodybuilding": return "bg-secondary text-secondary-content";
      case "General Fitness": return "bg-primary text-primary-content";
      case "Running": return "bg-success text-success-content";
      case "Biking": return "bg-warning text-warning-content";
      case "Swimming": return "bg-info text-info-content";
      default: return "bg-neutral text-neutral-content";
    }
  }

  function getNextWorkoutHighlights(program: TrainingProgramDTO): string[] {
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

  function calculateProgress(program: TrainingProgramDTO): number {
    const sessions = program.trainingSessions ?? [];
    const completed = sessions.filter(s => s.status === TrainingSessionStatus._2).length;
    return sessions.length === 0 ? 0 : Math.round((completed / sessions.length) * 100);
  }
</script>
<div class="p-6 max-w-6xl mx-auto text-base-content">
  <h1 class="text-3xl font-bold mb-6">{username}'s Program Library</h1>

  <div class="grid gap-6 md:grid-cols-2">
    {#each programs as program (program.trainingProgramID)}
      <div class="relative group">
        <div
          role="link"
          tabindex="0"
          class="card bg-base-100 text-base-content border border-base-300 shadow-md hover:shadow-lg transition cursor-pointer h-[320px]"
          on:click={() => goToProgram(program)}
          on:keydown={(e) => e.key === 'Enter' && goToProgram(program)}
        >
          <div class="card-body flex flex-col justify-between">
            <!-- Header -->
            <div class="flex items-center justify-between mb-2">
              <h2 class="card-title text-2xl md:text-3xl pb-6 truncate w-2/3">
                {program.title ?? 'Untitled'}
              </h2>
              <div class="flex items-center gap-2">
                <span class={`text-xs font-semibold px-3 py-1 rounded-full ${getTypeColor(program.tags?.[0] ?? '')}`}>
                  {program.tags?.[0] ?? 'Unknown'}
                </span>
                <button
                  type="button"
                  on:click|stopPropagation={() => program.trainingProgramID && deleteProgram(program.trainingProgramID)}
                  class="text-error hover:text-error-content text-xl font-bold"
                  title="Delete program"
                >
                  &times;
                </button>
              </div>
            </div>

            <!-- Dates and Highlights -->
            <div class="flex flex-col md:flex-row gap-4 text-sm">
              <div class="flex-1 space-y-1">
                <p><span class="font-semibold">Start:</span> {formatDate(program.startDate)}</p>
                <p><span class="font-semibold">End:</span> {formatDate(program.endDate)}</p>
              </div>

              <div class="flex-1">
                <h3 class="font-semibold mb-1">Next Workout Highlights:</h3>
                <ul class="space-y-1">
                  {#each getNextWorkoutHighlights(program) as line}
                    <li class="border-l-4 pl-3 border-base-300">{line}</li>
                  {/each}
                </ul>
              </div>
            </div>

            <!-- Progress -->
            <div>
              <p class="text-sm mt-3 mb-1 font-semibold">
                Next Workout: {formatDate(program.nextTrainingSessionDate)}
              </p>
              <progress class="progress w-full progress-primary" value={calculateProgress(program)} max="100"></progress>
              <p class="text-sm text-right mt-1">{calculateProgress(program)}%</p>
            </div>
          </div>
        </div>
      </div>
    {/each}
  </div>
</div>

<!-- Add Program Floating Button -->
<button
  on:click={() => showModal = true}
  class="fixed bottom-6 right-6 btn btn-primary btn-circle text-xl shadow-lg z-40"
>
  +
</button>

<CreateProgramModal
  show={showModal}
  on:close={() => showModal = false}
  on:created={handleProgramCreated}
/>
