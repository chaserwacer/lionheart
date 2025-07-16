<script lang="ts">
  import { page } from "$app/stores";
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import CreateSessionModal from "$lib/components/CreateSession.svelte";
  import { slugify } from "$lib/utils/slugify";
  import {
    TrainingProgramDTO,
    TrainingSessionDTO,
    TrainingSessionStatus,
    GetTrainingProgramEndpointClient,
  } from "$lib/api/ApiClient";

  const slug = $page.params.slug;
  let program: TrainingProgramDTO;
  let showModal = false;

  const baseUrl =
    typeof window !== "undefined"
      ? window.location.origin
      : "http://localhost:5174";

  import PlannedSessionViewer from "$lib/components/PlannedSessionViewer.svelte";
  import InProgressSessionViewer from "$lib/components/InProgressSessionViewer.svelte";
  import CompletedSessionViewer from "$lib/components/CompletedSessionViewer.svelte";
  import SkippedSessionViewer from "$lib/components/SkippedSessionViewer.svelte";
    import { load } from "../../+layout";

  // Group sessions into weekly buckets
  function getWeekStart(d: Date) {
  const dt = new Date(d);
  const day = dt.getDay(); // 0 = Sunday, 1 = Monday, ..., 6 = Saturday
  // If already Sunday (day=0), keep as is
  dt.setDate(dt.getDate() - day -1);

  return dt.toISOString().slice(0, 10);
}

  function groupSessionsByWeek(sessions: TrainingSessionDTO[]) {
  return sessions
    .slice()
    .sort((a, b) => {
      const dateA = new Date(a.date);
      const dateB = new Date(b.date);
      return dateA.getTime() - dateB.getTime();
    })
    .reduce(
      (acc, s) => {
        s.date.setDate(s.date.getDate() + 1); // Adjust to local time
        const wk = getWeekStart(new Date(s.date));
        
        (acc[wk] ||= []).push(s);
        return acc;
      },
      {} as Record<string, TrainingSessionDTO[]>,
    );
}
  function formatDateMonthDateYear(key: string) {
    var adjustedDate = new Date(key);
    adjustedDate.setDate(adjustedDate.getDate() + 1);
    return adjustedDate.toLocaleDateString(undefined, {
      month: "short",
      day: "numeric",
      year: "numeric",
    });
  }

let sessionsByWeek: Record<string, TrainingSessionDTO[]> = {};


  async function loadProgram() {
    const getProgramsClient = new GetTrainingProgramEndpointClient(baseUrl);

    try {
      program = await getProgramsClient.get(slug);
      if (program == null || program.trainingProgramID == null) {
        console.error("Program not found for slug:", slug);
        return;
      }
      sessionsByWeek = groupSessionsByWeek(program.trainingSessions);
      console.log("sessionsByWeek", sessionsByWeek);  
    } catch (error) {
      console.error("Failed to load program", error);
    }
  }

  onMount(async () => {
    await loadProgram();
  });



  

  
</script>

{#if program}
  <div class="p-6 max-w-6xl flex flex-col items-center justify-center mx-auto">
    <div class="flex justify-between items-center w-full">
      <a href="/programs" class="btn btn-sm btn-primary"> ← Programs </a>
      <h1 class="text-xl md:text-4xl font-extrabold mb-2 text-center">
        {program.title}
      </h1>
      <a
        class="btn btn-sm btn-primary"
        href={`/movementLib?returnTo=/programs/${slug}`}
      >
        Items →
      </a>
    </div>

    <div class="text-center items-center justify-center">
      {#each program.tags as tag}
        <span class="badge badge-primary">{tag}</span>
      {/each}
    </div>
    <div class="divider divider-2 divider-primary"></div>

    <div class="flex flex-col w-full gap-5">
      {#if program.trainingSessions.length}
        {#each Object.entries(sessionsByWeek) as sessionByWeek, index}
          <div class="flex flex-col gap-4">
            <h2 class="text-2xl font-bold">
              Week {index + 1}: {formatDateMonthDateYear(sessionByWeek[0])}
            </h2>

            <div
              class="flex flex-wrap flex-col md:flex-row items-center md:items-start gap-2"
            >
              {#each sessionByWeek[1] as session}
                <div>
                  <div class="">
                    {#if session.status === TrainingSessionStatus._1}
                      <InProgressSessionViewer {session} {slug} loadSessions={loadProgram}/>
                    {:else if session.status === TrainingSessionStatus._0}
                      <PlannedSessionViewer {session} {slug} loadSessions={loadProgram}/>
                    {:else if session.status === TrainingSessionStatus._2}
                      <CompletedSessionViewer {session} {slug} loadSessions={loadProgram}/>
                    {:else}
                      <SkippedSessionViewer {session} {slug} loadSessions={loadProgram}/>
                    {/if}
                  </div>
                </div>
              {/each}
            </div>
          </div>
          <div class="divider"></div>
        {/each}
      {/if}
    </div>
  </div>
{:else}
  <div class="p-6 max-w-4xl mx-auto flex items-center justify-center">
    <span class="loading loading-spinner loading-xs"></span>

  </div>
{/if}

<!-- Floating Add Button -->
<button
  on:click={() => (showModal = true)}
  class="fixed bottom-6 right-6 btn btn-circle btn-primary text-xl shadow-lg z-40"
>
  +
</button>

{#if program}
  <CreateSessionModal
    show={showModal}
    programID={program.trainingProgramID}
    existingSessionCount={program.trainingSessions.length}
    on:close={() => (showModal = false)}
    on:createdWithSession={() => {
      showModal = false;
      loadProgram();
    }}
  />
{/if}
