<script lang="ts">
  export let newEventNotes: string = "";
  export let newEventPain: number = 0;
  export let newEventTypeStr: "checkin" | "treatment" = "checkin";
  export let selectedSession: any | null = null;
  export let onOpenSessionPicker: () => void;
  export let onAdd: () => void;
  export let onCancel: () => void;

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
    <h2 class="text-xl font-bold mb-4">Add Injury Event</h2>

    <label class="label" for="eventNotes"><span class="label-text">Event Notes</span></label>
    <textarea id="eventNotes" class="textarea textarea-bordered mb-2" placeholder="Event Notes" bind:value={newEventNotes}></textarea>
    <label class="label" for="painLevelInput"><span class="label-text">Pain Level (0-10)</span></label>
    <input id="painLevelInput" class="input input-bordered mb-4" type="number" min="0" max="10" bind:value={newEventPain} placeholder="0-10" />
    <label class="label" for="injuryTypeSelect"><span class="label-text">Injury Event Type</span></label>
    <select id="injuryTypeSelect" class="select select-bordered mb-4" bind:value={newEventTypeStr}>
      <option value="checkin">Check-in</option>
      <option value="treatment">Treatment</option>
    </select>
    <button class="btn btn-outline w-full mb-2" on:click={onOpenSessionPicker}>Attach Session</button>
    {#if selectedSession}
      <p class="text-sm mb-2">Selected Session: #{selectedSession.sessionNumber} on {formatDate(selectedSession.date)}</p>
    {/if}
    <div class="flex gap-2">
      <button class="btn btn-info flex-1" on:click={onAdd}>Add</button>
      <button class="btn btn-outline flex-1" on:click={onCancel}>Cancel</button>
    </div>
  </div>
</div>
