<script lang="ts">
  import { describe } from "$lib/api/result";
  import { planProgram, type TrainingProgramView } from "$lib/api/training";
  import { toDateOnly } from "$lib/stores/training.svelte";

  // A program is always born planned; activating and completing it are transitions, so neither
  // the lifecycle state nor a summary belongs on this form.
  let {
    modalId = "create_program_modal",
    oncreated,
  }: { modalId?: string; oncreated?: (program: TrainingProgramView) => void } = $props();

  const fourWeeksOut = new Date();
  fourWeeksOut.setDate(fourWeeksOut.getDate() + 28);

  let title = $state("");
  let startDate = $state(toDateOnly(new Date()));
  let endDate = $state(toDateOnly(fourWeeksOut));
  let tagsText = $state("");
  let submitting = $state(false);
  let errorMsg = $state("");

  function dialog(): HTMLDialogElement | null {
    return document.getElementById(modalId) as HTMLDialogElement | null;
  }

  function close() {
    dialog()?.close();
  }

  async function submit() {
    errorMsg = "";

    if (title.trim() === "") {
      errorMsg = "A program needs a title.";
      return;
    }

    submitting = true;
    const result = await planProgram({
      title: title.trim(),
      startDate,
      endDate,
      tags: tagsText
        .split(",")
        .map((tag) => tag.trim())
        .filter((tag) => tag !== ""),
    });
    submitting = false;

    if (!result.ok) {
      errorMsg = describe(result.problem);
      return;
    }

    title = "";
    tagsText = "";
    oncreated?.(result.value);
    close();
  }
</script>

<dialog id={modalId} class="modal">
  <div class="modal-box border-2 border-base-content/10">
    <h3 class="font-display text-2xl font-black tracking-tight">New program</h3>

    <label class="label mt-5" for="{modalId}-title">
      <span class="label-text text-xs font-bold uppercase tracking-widest">Title</span>
    </label>
    <input
      id="{modalId}-title"
      type="text"
      bind:value={title}
      class="input input-bordered w-full"
      placeholder="Winter base"
    />

    <div class="mt-4 grid grid-cols-2 gap-3">
      <div>
        <label class="label" for="{modalId}-start">
          <span class="label-text text-xs font-bold uppercase tracking-widest">Starts</span>
        </label>
        <input
          id="{modalId}-start"
          type="date"
          bind:value={startDate}
          class="input input-bordered w-full"
        />
      </div>
      <div>
        <label class="label" for="{modalId}-end">
          <span class="label-text text-xs font-bold uppercase tracking-widest">Ends</span>
        </label>
        <input
          id="{modalId}-end"
          type="date"
          bind:value={endDate}
          class="input input-bordered w-full"
        />
      </div>
    </div>

    <label class="label mt-4" for="{modalId}-tags">
      <span class="label-text text-xs font-bold uppercase tracking-widest">Tags</span>
    </label>
    <input
      id="{modalId}-tags"
      type="text"
      bind:value={tagsText}
      class="input input-bordered w-full"
      placeholder="strength, base"
    />

    {#if errorMsg}
      <p class="mt-3 text-sm text-error">{errorMsg}</p>
    {/if}

    <div class="modal-action">
      <button type="button" onclick={close} class="btn btn-ghost min-h-11 h-11">Cancel</button>
      <button
        type="button"
        onclick={submit}
        disabled={submitting}
        class="btn btn-neutral min-h-11 h-11 rounded-xl px-6"
      >
        Plan program
      </button>
    </div>
  </div>

  <form method="dialog" class="modal-backdrop">
    <button aria-label="Close">close</button>
  </form>
</dialog>
