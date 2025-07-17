<script lang="ts">
  function formatNumber(num?: number): string {
    if (num === undefined || num === null) return "";
    // Show up to 2 decimals, but trim trailing zeroes
    return num.toFixed(2);
  }

  import { goto } from "$app/navigation";
  import {
    DeleteTrainingSessionEndpointClient,
    DuplicateTrainingSessionEndpointClient,
    MovementDTO,
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
    if (statusInt === TrainingSessionStatus._0) return "PLANNED";
    if (statusInt === TrainingSessionStatus._1) return "ACTIVE";
    if (statusInt === TrainingSessionStatus._2) return "COMPLETE";
    if (statusInt === TrainingSessionStatus._3) return "SKIPPED";
  }
  function formatSessionDate(date: string | Date) {
    const d = new Date(date);
    // d.setDate(d.getDate() + 1); // Adjust to local time
    return d.toLocaleDateString(undefined, {
      weekday: "short",
      month: "short",
      day: "numeric",
    });
  }
  async function deleteSession() {
    const confirmed = confirm("Are you sure you want to delete this session?");
    if (!confirmed || !session) return;
    const deleteClient = new DeleteTrainingSessionEndpointClient(baseUrl);
    try {
      await deleteClient.delete5(session.trainingSessionID);
      await loadSessions();
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

  // Calculate total tonnage for the session
  function getTotalTonnage(session: TrainingSessionDTO): number {
    return session.movements.reduce((total, m) => {
      const movementTonnage = m.sets.reduce(
        (sum, set) => sum + set.actualReps * set.actualWeight,
        0,
      );
      return total + movementTonnage;
    }, 0);
  }

  // Calculate average RPE for a movement
  function getAverageRPE(movement: MovementDTO): number | null {
    const rpes = movement.sets
      .map((s) => s.actualRPE)
      .filter((rpe) => rpe !== undefined && rpe !== null);
    if (rpes.length === 0) return null;
    return rpes.reduce((a, b) => a + b, 0) / rpes.length;
  }
</script>

<button
  type="button"
  class="hover:cursor-pointer card card-lg border border-base-300 rounded-xl text-left w-full focus:outline-none shadow-md"
  on:click={view}
  aria-label="View session details"
>
  <div
    class="card-body text-left pl-4 pt-4 pr-0 w-64 h-64 outline rounded-lg bg-success/10"
  >
    <div
      class="card-title flex justify-between items-center pr-4 text-xl font-bold"
    >
      <div class="flex flex-col">
    Session <div class="text-4xl">{session.sessionNumber}</div>
  </div>
      <div class="flex flex-col items-end gap-4">
        <div class="flex items-center gap-2">
          <div class="badge badge-success badge-md p-1 italic">
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
          <!-- <span class="ml-2"
            >Tonnage: <div>
              {getTotalTonnage(session).toLocaleString()}
            </div></span
          > -->
        </div>
      </div>
    </div>
    <div class="flex flex-col w-full pr-4 overflow-hidden text-ellipsis h-64" >
      <div class="flex pb-2">
        <div class="divider divider-neutral divider-end m-0 w-2/4"></div>
        <div class="divider divider-neutral m-0 w-2/4 text-xs">avg</div>
      </div>
      <table class="table-auto w-full text-xs table-fixed border-collapse">
        <thead>
          <tr class="text-left border-b border-base-300">
            <th class="w-1/6">Movement</th>
            <th class="w-1/12 text-center">Sets</th>
            <th class="w-1/12 text-center">Reps</th>
            <th class="w-1/12 text-center">Weight</th>
            <th class="w-1/12 text-center">RPE</th>
          </tr>
        </thead>
        <tbody>
          {#each session.movements as m}
            <tr class="border-b border-base-300">
              <td class="pr-2 break-words whitespace-normal w-1/2">
                {m.movementBase.name}
              </td>
              <td class="text-center">{m.sets.length}</td>
              <td class="text-center">
                {#if m.sets.length > 0}
                  {(
                    m.sets.reduce((sum, set) => sum + set.actualReps, 0) /
                    m.sets.length
                  ).toFixed(1)}
                {/if}
              </td>
              <td class="text-center">
                {#if m.sets.length > 0}
                  {(
                    m.sets.reduce((sum, set) => sum + set.actualWeight, 0) /
                    m.sets.length
                  ).toFixed(1)}
                {/if}
              </td>
              <td class="text-center">
                {#if getAverageRPE(m) !== null}
                  {(getAverageRPE(m) ?? 0).toFixed(1)}
                {:else}
                  -
                {/if}
              </td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  </div>
</button>
