<script lang="ts">
  import { planSession, startUnplannedSession, type TrainingSessionView } from "$lib/api/training";
  import { describe } from "$lib/api/result";
  import { toDateOnly } from "$lib/stores/training.svelte";

  // Sessions are created by choosing which state they start in — there is no status field to set
  // afterwards. Planning and starting are separate routes because they are different facts.
  let {
    modalId = "create_session_modal",
    trainingProgramId = null,
    oncreated,
  }: {
    modalId?: string;
    trainingProgramId?: string | null;
    oncreated?: (session: TrainingSessionView) => void;
  } = $props();

  type Intent = "planned" | "active";

  let intent = $state<Intent>("planned");
  let plannedFor = $state(toDateOnly(new Date()));
  let notes = $state("");
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
    submitting = true;

    const result =
      intent === "planned"
        ? await planSession({ plannedFor, prescriptions: [], notes, trainingProgramId })
        : await startUnplannedSession({
            startedAt: new Date().toISOString(),
            notes,
            trainingProgramId,
          });

    submitting = false;

    if (!result.ok) {
      errorMsg = describe(result.problem);
      return;
    }

    notes = "";
    oncreated?.(result.value);
    close();
  }
</script>

<dialog id={modalId} class="modal">
  <div class="modal-box border-2 border-base-content/10">
    <h3 class="font-display text-2xl font-black tracking-tight">New session</h3>

    <div class="mt-5 flex gap-2">
      <button
        type="button"
        onclick={() => (intent = "planned")}
        class="btn btn-sm min-h-11 h-11 flex-1 rounded-xl border-2 normal-case
               {intent === 'planned'
          ? 'border-base-content bg-base-content text-base-100'
          : 'border-base-content/15 bg-base-100'}"
      >
        Plan for a day
      </button>
      <button
        type="button"
        onclick={() => (intent = "active")}
        class="btn btn-sm min-h-11 h-11 flex-1 rounded-xl border-2 normal-case
               {intent === 'active'
          ? 'border-base-content bg-base-content text-base-100'
          : 'border-base-content/15 bg-base-100'}"
      >
        Start now
      </button>
    </div>

    {#if intent === "planned"}
      <label class="label mt-4" for="{modalId}-date">
        <span class="label-text text-xs font-bold uppercase tracking-widest">Planned for</span>
      </label>
      <input
        id="{modalId}-date"
        type="date"
        bind:value={plannedFor}
        class="input input-bordered w-full"
      />
    {/if}

    <label class="label mt-4" for="{modalId}-notes">
      <span class="label-text text-xs font-bold uppercase tracking-widest">Notes</span>
    </label>
    <textarea
      id="{modalId}-notes"
      bind:value={notes}
      rows="3"
      class="textarea textarea-bordered w-full"
      placeholder="Optional"
    ></textarea>

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
        {intent === "planned" ? "Plan it" : "Start it"}
      </button>
    </div>
  </div>

  <form method="dialog" class="modal-backdrop">
    <button aria-label="Close">close</button>
  </form>
</dialog>
