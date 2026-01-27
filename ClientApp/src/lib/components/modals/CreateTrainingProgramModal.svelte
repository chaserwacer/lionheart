<script lang="ts">
  import { createEventDispatcher } from "svelte";
  import {
    CreateTrainingProgramEndpointClient,
    CreateTrainingProgramRequest
  } from "$lib/api/ApiClient";

  const dispatch = createEventDispatcher();

  export let modalId = "create_program_modal";

  let title = "";
  let startDateStr = new Date().toISOString().slice(0, 10);
  let endDateStr = new Date(Date.now() + 1000 * 60 * 60 * 24 * 28).toISOString().slice(0, 10); // +4 weeks
  let tagsText = ""; // comma-separated

  let isSubmitting = false;
  let errorMsg = "";

  function getDialog(): HTMLDialogElement | null {
    return document.getElementById(modalId) as HTMLDialogElement | null;
  }

  function closeModal() {
    getDialog()?.close();
  }

  function toSafeDateTime(dateOnlyStr: string): Date {
    // Noon local time avoids DST edge cases
    return new Date(`${dateOnlyStr}T12:00:00`);
  }

  function parseTags(input: string): string[] {
    return input
      .split(",")
      .map(t => t.trim())
      .filter(Boolean);
  }

  async function onSubmit() {
    errorMsg = "";

    if (!title.trim()) {
      errorMsg = "Program title is required.";
      return;
    }

    const sd = toSafeDateTime(startDateStr);
    const ed = toSafeDateTime(endDateStr);

    if (Number.isNaN(sd.getTime()) || Number.isNaN(ed.getTime())) {
      errorMsg = "Invalid start or end date.";
      return;
    }

    if (sd > ed) {
      errorMsg = "Start date must be on/before end date.";
      return;
    }

    const req: CreateTrainingProgramRequest = {
      title: title.trim(),
      startDate: sd,
      endDate: ed,
      tags: parseTags(tagsText)
    } as any;

    isSubmitting = true;
    try {
      const client = new CreateTrainingProgramEndpointClient();
      const res = await client.post(req);

      if (!res) throw new Error("No response from server.");

      closeModal();
      dispatch("created", { program: res });

      location.reload();
    } catch (err: any) {
      errorMsg =
        err?.body?.title ||
        err?.body?.detail ||
        err?.message ||
        "Failed to create program.";
    } finally {
      isSubmitting = false;
    }
  }
</script>

<dialog id={modalId} class="modal">
  <div class="modal-box bg-base-100 rounded-2xl border border-base-content/10 shadow-xl">
    <div class="flex items-start justify-between gap-4">
      <div>
        <h3 class="text-2xl font-display font-black">Create Program</h3>
        <p class="text-sm text-base-content/60 mt-1">
          Title, date range, and optional tags.
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
        <div class="label"><span class="label-text">Title</span></div>
        <input
          class="input input-bordered rounded-xl"
          placeholder="e.g., Strength Block"
          bind:value={title}
        />
      </label>

      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <label class="form-control">
          <div class="label"><span class="label-text">Start Date</span></div>
          <input class="input input-bordered rounded-xl" type="date" bind:value={startDateStr} />
        </label>

        <label class="form-control">
          <div class="label"><span class="label-text">End Date</span></div>
          <input class="input input-bordered rounded-xl" type="date" bind:value={endDateStr} />
        </label>
      </div>

      <label class="form-control">
        <div class="label">
          <span class="label-text">Tags</span>
          <span class="label-text-alt text-base-content/40">Comma separated (optional)</span>
        </div>
        <input
          class="input input-bordered rounded-xl"
          placeholder="Powerlifting, Bodybuilding, Running"
          bind:value={tagsText}
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
