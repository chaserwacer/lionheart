<script lang="ts">
  import { goto } from '$app/navigation';
  import { TrainingSessionStatus, type TrainingSessionDTO } from '$lib/api/ApiClient';
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
</script>

<button
  type="button"
  class="hover:cursor-pointer card card-lg border border-warning rounded-xl text-left w-full focus:outline-none shadow-md opacity-60"
  on:click={view}
  aria-label="View skipped session details"
>
  <div class="card-body text-left pl-4 pt-4 pr-0 w-64 h-64 outline rounded-lg bg-neutral/10">
    <h2 class="card-title flex justify-between items-center pr-4 text-xl ">
      Session {session.sessionNumber}
      <div class="flex items-center gap-2">
        <div class="badge badge-neutral badge-md p-1 italic">
          {getSessionStatus(session.status)}
        </div>
      </div>
    </h2>
    <div class="text-sm text-base-content/60 flex items-center pr-4">
      {formatSessionDate(session.date)}
    </div>

    <div class="flex flex-col w-full pr-4 gap-2">
      <div class="divider divider-neutral m-0 w-full"></div>
      <table class="table-auto w-full text-xs table-fixed border-collapse">
        <thead>
          <tr class="text-left border-b border-base-300">
            <th class="w-2/3">Movement</th>
            <th class="w-1/3 text-center">Sets</th>
          </tr>
        </thead>
        <tbody>
          {#each session.movements as m}
            <tr class="border-b border-base-300">
              <td class="pr-2 break-all w-2/3">{m.movementBase.name}</td>
              <td class="text-center w-1/3">{m.sets.length}</td>
            </tr>
          {/each}
        </tbody>
      </table>
      <div class="text-lg italic  mt-2">This session was skipped</div>
    </div>
</button>
