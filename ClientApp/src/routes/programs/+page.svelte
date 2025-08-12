<script lang="ts">
  import { onMount } from "svelte";
  import { slugify } from "$lib/utils/slugify";
  import { goto } from "$app/navigation";
  import CreateProgramModal from "$lib/components/CreateProgram.svelte";
  import TrainingProgramViewer from "$lib/components/TrainingProgramViewer.svelte";

  import {
    TrainingProgramDTO,
    TrainingSessionStatus,
    GetTrainingProgramsEndpointClient,
    DeleteTrainingProgramEndpointClient,
    TrainingSessionDTO,
    GetTrainingProgramEndpointClient,
  } from "$lib/api/ApiClient";
  import { bootUserDto } from "$lib/stores/stores";
  import PlannedSessionViewer from "$lib/components/PlannedSessionViewer.svelte";
  import CompleteSessionViewer from "$lib/components/CompletedSessionViewer.svelte";

  let showModal = false;
  let programs: TrainingProgramDTO[] = [];
  let programsDisplaySessions: Record<
    string,
    [TrainingSessionDTO, TrainingSessionDTO]
  > = {};
  const baseUrl =
    typeof window !== "undefined"
      ? window.location.origin
      : "http://localhost:5174";

  onMount(async () => {
    await loadPrograms();
  });

  async function loadPrograms() {
    try {
      const getProgramsClient = new GetTrainingProgramsEndpointClient(baseUrl);
      programs = await getProgramsClient.getAll4();
      programsDisplaySessions = {};
      programs.forEach((element) => {
        for (const session of element.trainingSessions) {
          session.date = new Date(session.date);
          session.date.setDate(session.date.getDate() + 1);
        }
        programsDisplaySessions[element.trainingProgramID] =
          getRelevantSessions(element);
      });
      console.log("programs", programs);
      console.log("programsDisplaySessions", programsDisplaySessions);
    } catch (error) {
      console.error("Failed to load programs:", error);
    }
  }

  async function handleProgramCreated() {
    await loadPrograms();
    showModal = false;
  }

  async function deleteProgram(programID: string) {
    const confirmed = confirm("Are you sure you want to delete this program?");
    if (!confirmed) return;

    try {
      const deleteProgramClient = new DeleteTrainingProgramEndpointClient(
        baseUrl,
      );
      await deleteProgramClient.delete6(programID);
      programs = programs.filter((p) => p.trainingProgramID !== programID);
    } catch (err) {
      alert("Failed to delete program.");
      console.error(err);
    }
  }

  function goToProgram(program: TrainingProgramDTO) {
    goto(`/programs/${slugify(program.trainingProgramID)}`);
  }

  function formatDate(dateInput: Date | string | undefined): string {
    if (!dateInput) return "—";
    // If input is a string, extract only the date part
    const dateStr =
      typeof dateInput === "string"
        ? dateInput.slice(0, 10)
        : dateInput.toISOString().slice(0, 10);
    // Create a new Date object as local time
    const date = new Date(dateStr + "T00:00:00");
    const options = { month: "short", day: "numeric" } as const;
    return date.toLocaleDateString(undefined, options);
  }

  function getTypeColor(type: string) {
    switch (type) {
      case "Powerlifting":
        return "bg-error text-error-content";
      case "Bodybuilding":
        return "bg-secondary text-secondary-content";
      case "General Fitness":
        return "bg-primary text-primary-content";
      case "Running":
        return "bg-success text-success-content";
      case "Biking":
        return "bg-warning text-warning-content";
      case "Swimming":
        return "bg-info text-info-content";
      default:
        return "bg-neutral text-neutral-content";
    }
  }

  function getRelevantSessions(
    program: TrainingProgramDTO,
  ): [TrainingSessionDTO, TrainingSessionDTO] {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    // Most recent completed session before today
    const completedSessions = program.trainingSessions
      .filter(
        (session) =>
          session.status === TrainingSessionStatus._2 &&
          new Date(session.date).setHours(0, 0, 0, 0) <= today.getTime(),
      )
      .sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime());
    // Next planned session today or later
    const plannedSessions = program.trainingSessions
      .filter(
        (session) =>
          session.status === TrainingSessionStatus._0 &&
          new Date(session.date) >= today,
      )
      .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime());
    console.log("completedSessions", completedSessions);
    return [completedSessions[0], plannedSessions[0]];
  }

  function calculateProgress(program: TrainingProgramDTO): number {
    const sessions = program.trainingSessions;
    const completed = sessions.filter(
      (s) => s.status === TrainingSessionStatus._2,
    ).length;
    return sessions.length === 0
      ? 0
      : Math.round((completed / sessions.length) * 100);
  }
</script>

<div class="p-6 max-w-6xl mx-auto text-base-content">
  <h1 class="text-3xl font-bold mb-6">{$bootUserDto.name}'s Program Library</h1>

  <div class="flex flex-col gap-8 w-full">
    {#each programs as program}
      <TrainingProgramViewer
        {program}
        {loadPrograms}
      />
    {/each}
  </div>
</div>

<!-- Add Program Floating Button -->
<button
  on:click={() => (showModal = true)}
  class="fixed bottom-6 right-6 btn btn-primary btn-circle text-xl shadow-lg z-40"
>
  +
</button>

<CreateProgramModal
  show={showModal}
  on:close={() => (showModal = false)}
  on:created={handleProgramCreated}
/>
