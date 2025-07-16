<script lang="ts">
  import { goto } from '$app/navigation';
  import {
    TrainingSessionStatus,
    type TrainingSessionDTO,
    type MovementDTO,
  } from '$lib/api/ApiClient';
  export let session: TrainingSessionDTO;
  export let slug: string;

  function view() {
    goto(`/programs/${slug}/session/${session.trainingSessionID}`);
  }

  function getSessionStatus(status: TrainingSessionStatus) {
    if (status === undefined) return 'Unknown';
    const statusInt = status.valueOf();
    if (statusInt === TrainingSessionStatus._0) return 'PLANNED';
    if (statusInt === TrainingSessionStatus._1) return 'ACTIVE';
    if (statusInt === TrainingSessionStatus._2) return 'COMPLETED';
    if (statusInt === TrainingSessionStatus._3) return 'SKIPPED';
  }

  function formatSessionDate(date: string | Date) {
    const d = new Date(date);
    // d.setDate(d.getDate() + 1); // Adjust to local time
    return d.toLocaleDateString(undefined, {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
    });
  }

  function getAverageRPE(movement: MovementDTO): number | null {
    const rpes = movement.sets
      .map(s => s.actualRPE)
      .filter(rpe => rpe !== undefined && rpe !== null);
    if (rpes.length === 0) return null;
    return rpes.reduce((a, b) => a + b, 0) / rpes.length;
  }

  // Partition movements
  $: completedMovements = session.movements.filter(m => m.isCompleted);
  $: uncompletedMovements = session.movements.filter(m => !m.isCompleted);
</script>

<button
  type="button"
  class="hover:cursor-pointer card card-lg border border-base-300 rounded-xl text-left w-full focus:outline-none shadow-md"
  on:click={view}
  aria-label="View session details"
>
  <div class="card-body text-left pl-4 pt-4 pr-0 w-64 h-64 outline rounded-lg bg-info/10">
    <h2 class="card-title flex justify-between items-center pr-4 text-xl">
      Session {session.sessionNumber}
      <div class="flex items-center gap-2">
        <div class="badge badge-info badge-md p-1 italic">
          {getSessionStatus(session.status)}
        </div>
      </div>
    </h2>
    <div class="text-sm text-base-content/60 flex items-center pr-4 justify-between">
      {formatSessionDate(session.date)}
      <progress class="progress progress-xs w-24"></progress>
    </div>

    <div class="flex flex-col w-full pr-4 gap-2">
      <div class="divider divider-neutral m-0 w-full "></div>
      <table class="table-auto w-full text-xs table-fixed border-collapse ">
        <thead>
          <tr class="text-left border-b border-base-300">
            <th class="w-2/3">Movement</th>
            <th class="w-1/6 text-center">Sets</th>
            <th class="w-1/6 text-center">Status</th>
          </tr>
        </thead>
        <tbody>
          {#each completedMovements as m}
            <tr class="border-b border-base-300 bg-success/20 font-semibold">
              <td class="pr-2 break-all w-2/3">{m.movementBase.name}</td>
              <td class="text-center w-1/6">{m.sets.length}</td>
              <td class="text-center w-1/6 text-success font-bold">✔</td>
            </tr>
          {/each}
          {#each uncompletedMovements as m}
            <tr class="border-b border-base-300 opacity-70">
              <td class="pr-2 break-all w-2/3">{m.movementBase.name}</td>
              <td class="text-center w-1/6">{m.sets.length}</td>
              <td class="text-center w-1/6 text-base-content/50">—</td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  </div>
</button>
