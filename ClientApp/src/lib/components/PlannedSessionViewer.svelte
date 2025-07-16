<script lang="ts">
  import { goto } from "$app/navigation";
  import {
    DeleteTrainingSessionEndpointClient,
    TrainingSessionStatus,
    type TrainingSessionDTO,
  } from "$lib/api/ApiClient";
  export let session: TrainingSessionDTO;
  export let loadSessions: () => Promise<void>;
  export let slug: string;
  const baseUrl =
    typeof window !== "undefined"
      ? window.location.origin
      : "http://localhost:5174";

  function view() {
    goto(`/programs/${slug}/session/${session.trainingSessionID}`);
  }
  function getSessionStatus(status: TrainingSessionStatus) {
    if (status === undefined) return "Unknown";
    const statusInt = status.valueOf();

    // Map the enum value to a string
    if (statusInt === TrainingSessionStatus._0) return "PLANNED";
    if (statusInt === TrainingSessionStatus._1) return "ACTIVE";
    if (statusInt === TrainingSessionStatus._2) return "COMPLETED";
    if (statusInt === TrainingSessionStatus._3) return "SKIPPED";
  }

  function formatSessionDate(date: string | Date) {
    const d = new Date(date);
    return d.toLocaleDateString(undefined, {
      weekday: "short",
      month: "short",
      day: "numeric",
    });
  }

  // Stub: Generate a consideration for this session
  function getConsideration(session: TrainingSessionDTO): string {
    // TODO: Replace with real logic
    return "Stay hydrated and focus on form!";
  }

  async function deleteSession() {
    const confirmed = confirm("Are you sure you want to delete this session?");
    if (!confirmed || !session) return;

    const deleteClient = new DeleteTrainingSessionEndpointClient(baseUrl);
    try {
      await deleteClient.delete5(session.trainingSessionID);
      await loadSessions(); // Reload sessions to reflect changes
    } catch {
      alert("Failed to delete session.");
    }
  }
</script>

<button
  type="button"
  class="hover:cursor-pointer card card-lg border border-base-300 rounded-xl text-left w-full focus:outline-none shadow-md"
  on:click={view}
  aria-label="View session details"
>
  <div class="card-body text-left pl-4 pt-4 pr-0 w-64 h-64 outline rounded-lg">
    <h2 class="card-title flex gap-2">
      Session {session.sessionNumber}
      <div class="badge badge-primary badge-md p-1 italic">
        {getSessionStatus(session.status)}
      </div>
      <button
        class="btn btn-xs btn-error w-6 h-6 min-w-0 min-h-0 p-0 flex items-center justify-center rounded-sm"
        on:click={(e) => {
          e.stopPropagation(); // 👈 Prevents triggering card click
          deleteSession();
        }}>x</button
      >
    </h2>
    <div class="text-sm text-base-content/60 flex items-center">
      {formatSessionDate(session.date)}
    </div>

    <div class="flex flex-row">
      <!-- Movements Section -->
      <div class="flex flex-col grow-3 pr-5 max-w-24">
        <h3 class="font-semibold text-sm mb-1 ">Movements</h3>
        <div class="divider m-0 w-23 "></div>
        <ul class="text-sm space-y-1 ">
          {#each session.movements as m}
            <li class="break-all">{m.sets.length} x {m.movementBase.name}</li>
          {/each}
        </ul>
      </div>

      <!-- Considerations Section -->
      <div class="flex flex-col border-l pl-4 flex-1 ">
        <h3 class="font-semibold text-sm mb-1">Considerations</h3>
        <div class="divider m-0 w-24"></div>
        <div class="text-xs text-base-content/80 italic w-24">
          {getConsideration(session)}
        </div>
      </div>
    </div>
  </div>
</button>
