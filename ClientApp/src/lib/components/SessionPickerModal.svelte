<script lang="ts">
  export let programs: any[] = [];
  export let selectedProgram: any | null = null;
  export let selectedSession: any | null = null;
  export let onClose: () => void;

  function chooseSession(session: any) {
    selectedSession = session;
    onClose?.();
  }

  function formatDate(d?: string | Date | null) {
    if (!d) return "";
    try {
      const dt = typeof d === "string" ? new Date(d) : d;
      if (Number.isNaN(dt.getTime())) return String(d ?? "");
      return dt.toLocaleDateString();
    } catch {
      return String(d ?? "");
    }
  }
</script>

<div class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
  <div class="card bg-base-100 p-6 w-full max-w-md shadow-xl border border-base-300">
    {#if !selectedProgram}
      <h2 class="text-lg font-bold mb-4">Select Program</h2>
      {#each programs as program}
        <button type="button" class="btn btn-block mb-2" on:click={() => (selectedProgram = program)}>{program.title}</button>
      {/each}
    {:else}
      <h2 class="text-lg font-bold mb-4">Select Session</h2>
      {#each (selectedProgram.trainingSessions ?? []) as session}
        <button type="button" class="btn btn-outline btn-block mb-2" on:click={() => chooseSession(session)}>
          Session #{session.sessionNumber} - {formatDate(session.date)}
        </button>
      {/each}
      <button class="btn btn-outline mt-2 w-full" on:click={() => (selectedProgram = null)}>← Back to Programs</button>
    {/if}
  </div>
</div>
