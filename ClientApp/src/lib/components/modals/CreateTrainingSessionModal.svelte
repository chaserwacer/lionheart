<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import {
    CreateTrainingSessionEndpointClient,
    CreateTrainingSessionRequest
  } from "$lib/api/ApiClient";

  const dispatch = createEventDispatcher();

  export let modalId = "create_session_modal";
  export let trainingProgramId: string | null = null; // optional: null = standalone

  let dateStr = new Date().toISOString().slice(0, 10); // yyyy-mm-dd
  let notes = "";

  // keep hidden for now unless you add UI
  let perceivedEffortRatings: any = null;

  let isSubmitting = false;
  let errorMsg = "";

  function getDialog(): HTMLDialogElement | null {
    return document.getElementById(modalId) as HTMLDialogElement | null;
  }

  export function openModal() {
    errorMsg = "";
    isSubmitting = false;
    getDialog()?.showModal();
  }

  function closeModal() {
    getDialog()?.close();
  }

  function toSafeDateTime(dateOnlyStr: string): Date {
    // Noon local time avoids DST midnight weirdness
    return new Date(`${dateOnlyStr}T12:00:00`);
  }

  

  async function onSubmit() {
    errorMsg = "";

    const d = toSafeDateTime(dateStr);
    if (Number.isNaN(d.getTime())) {
      errorMsg = "Invalid date.";
      return;
    }

    // Align to backend record:
    // CreateTrainingSessionRequest(DateTime Date, Guid? TrainingProgramID, string Notes, PerceivedEffortRatings? PerceivedEffortRatings)
    const req: CreateTrainingSessionRequest = {
      date: d,
      trainingProgramID: trainingProgramId ?? null,
      notes: notes ?? "",
      perceivedEffortRatings: perceivedEffortRatings ?? null
    } as any;

    isSubmitting = true;
    try {
      const client = new CreateTrainingSessionEndpointClient();
      const res = await client.post(req);

      if (!res) throw new Error("No response from server.");

      closeModal();
      dispatch("created", { session: res });

      // your preferred “truth refresh”
      location.reload();
    } catch (err: any) {
      errorMsg =
        err?.body?.title ||
        err?.body?.detail ||
        err?.message ||
        "Failed to create session.";
    } finally {
      isSubmitting = false;
    }
  }
</script>

<dialog id={modalId} class="modal">
  <div class="modal-box bg-base-100 rounded-2xl border border-base-content/10 shadow-xl">
    <div class="flex items-start justify-between gap-4">
      <div>
        <h3 class="text-2xl font-display font-black">
          {#if trainingProgramId}Add Program Session{:else}Add Training Session{/if}
        </h3>
        <p class="text-sm text-base-content/60 mt-1">
          {#if trainingProgramId}
            This session will be added to the current program.
          {:else}
            This will create a standalone session.
          {/if}
        </p>
      </div>

      <button class="btn btn-ghost btn-sm rounded-xl" on:click={closeModal} aria-label="Close">
        ✕
      </button>
    </div>

    {#if errorMsg}
      <div class="alert alert-error mt-4 rounded-xl">
        <span>{errorMsg}</span>
      </div>
    {/if}

    <div class="mt-6 space-y-4">
      <label class="form-control">
        <div class="label"><span class="label-text">Session Date</span></div>
        <input class="input input-bordered rounded-xl" type="date" bind:value={dateStr} />
      </label>

      <label class="form-control">
        <div class="label">
          <span class="label-text">Notes</span>
          <span class="label-text-alt text-base-content/40">Optional</span>
        </div>
        <textarea
          class="textarea textarea-bordered rounded-xl min-h-[96px]"
          placeholder="Optional notes for this session..."
          bind:value={notes}
        />
      </label>
    </div>

    <div class="modal-action mt-6">
      <button class="btn btn-outline rounded-xl" on:click={closeModal} disabled={isSubmitting}>
        Cancel
      </button>
      <button class="btn btn-primary rounded-xl" on:click={onSubmit} disabled={isSubmitting}>
        {#if isSubmitting}Creating...{:else}Create{/if}
      </button>
    </div>
  </div>

  <form method="dialog" class="modal-backdrop">
    <button on:click={closeModal}>close</button>
  </form>
</dialog>
