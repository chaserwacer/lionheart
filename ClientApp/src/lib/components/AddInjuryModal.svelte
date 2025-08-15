<script lang="ts">
  export let newName: string = "";
  export let newDate: string = new Date().toISOString().slice(0, 10);
  export let newNotes: string = "";
  export let onCreate: () => void;
  export let onCancel: () => void;
</script>

<div class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
  <div class="card bg-base-100 p-6 w-full max-w-md shadow-xl border border-base-300">
    <h2 class="text-xl font-bold mb-4">Add Injury</h2>
    <input class="input input-bordered mb-2" type="text" bind:value={newName} placeholder="Injury Name" required />
    <label class="label" for="newDate"><span class="label-text">Date</span></label>
    <input id="newDate" class="input input-bordered mb-2" type="date" bind:value={newDate} />
    <label class="label" for="newNotes"><span class="label-text">Notes</span></label>
    <textarea id="newNotes" class="textarea textarea-bordered mb-4" placeholder="Notes" bind:value={newNotes}></textarea>
    <div class="flex gap-2">
      <button class="btn btn-accent flex-1" disabled={!newName?.trim()} on:click={onCreate}>Create</button>
      <button class="btn btn-outline flex-1" on:click={onCancel}>Cancel</button>
    </div>
  </div>
 </div>
