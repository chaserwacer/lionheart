<script lang="ts">
  export let selectedEvent: {
    notes?: string;
    painLevel?: number;
    injuryType?: any;
    creationTime?: string;
    trainingSessionID?: string | number;
  } | null = null;
  export let onClose: () => void;

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

{#if selectedEvent}
  <div class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
    <div class="card bg-base-100 p-6 w-full max-w-md shadow-xl border border-base-300">
      <h2 class="text-xl font-bold mb-4">Injury Event</h2>
      <div class="space-y-2 text-sm">
        <div class="flex justify-between"><span class="opacity-70">Session</span><span>{selectedEvent.trainingSessionID ?? "N/A"}</span></div>
        <div class="flex justify-between"><span class="opacity-70">Date</span><span>{formatDate(selectedEvent.creationTime)}</span></div>
        <div class="flex justify-between"><span class="opacity-70">Pain Level</span><span>{selectedEvent.painLevel}</span></div>
        <div class="flex justify-between"><span class="opacity-70">Type</span><span>{String(selectedEvent.injuryType)}</span></div>
        <div>
          <div class="opacity-70 mb-1">Notes</div>
          <div class="p-3 rounded-box bg-base-200 whitespace-pre-wrap">{selectedEvent.notes}</div>
        </div>
      </div>
      <div class="mt-4">
        <button class="btn btn-outline w-full" on:click={onClose}>Close</button>
      </div>
    </div>
  </div>
{/if}
