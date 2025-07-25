<script lang="ts">
  function formatNumber(num?: number): string {
    if (num === undefined || num === null) return "";
    return num.toFixed(2);
  }
  import { goto } from "$app/navigation";
  import {
    DeleteTrainingSessionEndpointClient,
    DuplicateTrainingSessionEndpointClient,
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
    if (statusInt === TrainingSessionStatus._4) return "AI Modified";
  }

  function formatSessionDate(date: string | Date) {
    const d = new Date(date);
    //d.setDate(d.getDate() + 1); // Adjust to local time
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
      await deleteClient.delete6(session.trainingSessionID);
      await loadSessions(); // Reload sessions to reflect changes
    } catch {
      alert("Failed to delete session.");
    }
  }

  async function duplicateSession() {
    const duplicateClient = new DuplicateTrainingSessionEndpointClient(baseUrl);
    try {
      await duplicateClient.duplicate(session.trainingSessionID);
      await loadSessions();
    } catch {
      alert("Failed to duplicate session.");
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
    <div
      class="card-title flex justify-between items-center pr-4 text-xl font-bold"
    >
      <!-- Session number stays left -->
      <div class="flex flex-col">
        Session <div class="text-4xl">{session.sessionNumber}</div>
      </div>

      <!-- Badge and delete button grouped right -->
      <div class="flex flex-col items-end gap-4">
        <div class="flex items-center gap-2">
          <div class="badge badge-accent badge-md p-1 italic">
            {getSessionStatus(session.status)}
          </div>
          <button
            class="btn btn-xs outline rounded-md w-5 h-5 min-w-0 min-h-0 p-0 flex items-center justify-center rounded-xs hover:btn-accent"
            on:click={(e) => {
              e.stopPropagation();
              duplicateSession();
            }}>⧉</button
          >
          <button
            class="btn btn-xs outline rounded-md w-5 h-5 min-w-0 min-h-0 p-0 flex items-center justify-center rounded-xs hover:btn-error"
            on:click={(e) => {
              e.stopPropagation();
              deleteSession();
            }}>x</button
          >
        </div>
        <div class="text-md text-base-content/60 flex items-center">
          {formatSessionDate(session.date)}
        </div>
      </div>
    </div>
    <div class="divider divider-neutral m-0 w-full pr-4"></div>
    <div
      class="flex flex-row justify-between w-full pr-4 overflow-hidden text-ellipsis h-64"
    >
      <!-- Movements Section -->
      <div class="flex flex-col h-full w-1/2 pr-1">
        <h3 class="font-semibold text-sm mb-1 ">Movements</h3>

        <div class="text-xs space-y-1 flex-1 overflow-hidden">
          {#each session.movements as m}
            <div class="break-all truncate">
              {m.sets.length} x {m.movementBase.name}
            </div>
          {/each}
        </div>
      </div>

      <!-- Considerations Section -->
      <div class="flex flex-col h-full w-1/2 pl-1 ">
        <h3 class="font-semibold text-sm mb-1 ">Notes</h3>
 
        <div
          class="text-xs text-base-content/80 italic w-full break-words overflow-hidden "
          style="max-height: 100%;"
        >
          {session.notes}
        </div>
      </div>
    </div>
  </div>
</button>
