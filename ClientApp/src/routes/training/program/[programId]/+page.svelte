<script lang="ts">
  import { page } from "$app/stores";
  import { goto } from "$app/navigation";
  import { onMount } from "svelte";
  import CreateTrainingSessionModal from "$lib/components/modals/CreateTrainingSessionModal.svelte";
  import {
    GetTrainingProgramEndpointClient,
    GetTrainingSessionsEndpointClient,
    TrainingProgramDTO,
    TrainingSessionDTO,
    UpdateTrainingProgramEndpointClient,
    UpdateTrainingProgramRequest,
    UpdateTrainingSessionEndpointClient,
    UpdateTrainingSessionRequest,
    DeleteTrainingSessionEndpointClient,
    DuplicateTrainingSessionEndpointClient,
    DeleteTrainingProgramEndpointClient,
  } from "$lib/api/ApiClient";

  let createSessionModal: any;
  $: programId = $page.params.programId;

  let program: TrainingProgramDTO | null = null;

  // canonical list (used for API updates)
  let sessions: TrainingSessionDTO[] = [];

  let loading = true;
  let errorMsg = "";
  let isEditing = false;

  let draftTitle = "";
  let draftStartDate = "";
  let draftEndDate = "";

  type SessionEdit = { notes: string };
  let sessionEdits: Record<string, SessionEdit> = {};

  // sessionId -> newDate (YYYY-MM-DD)
  let pendingReorder: Record<string, string> = {};

  function goBack() {
    goto("/training");
  }

  function openAddSession() {
    createSessionModal?.openModal();
  }

  function openSession(sessionId?: string) {
    if (!sessionId) return;
    goto(`/training/program/${programId}/session/${sessionId}`);
  }

  function formatProgramRange(startRaw: any, endRaw: any): string {
    const start =
      startRaw instanceof Date
        ? startRaw
        : new Date(`${toIsoDateOnly(startRaw)}T12:00:00`);
    const end =
      endRaw instanceof Date
        ? endRaw
        : new Date(`${toIsoDateOnly(endRaw)}T12:00:00`);

    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return "";

    const startStr = start.toLocaleDateString(undefined, {
      month: "short",
      day: "numeric",
    });
    const endStr = end.toLocaleDateString(undefined, {
      month: "short",
      day: "numeric",
    });

    return `${startStr} - ${endStr}`;
  }

  function formatSessionDate(raw: any): string {
    const d =
      raw instanceof Date ? raw : new Date(`${toIsoDateOnly(raw)}T12:00:00`);
    if (Number.isNaN(d.getTime())) return String(raw);

    const month = String(d.getMonth() + 1); // no pad => 1..12
    const day = String(d.getDate()); // no pad => 1..31
    return `${month}/${day}`;
  }

  function dateClosenessSortKey(dateStr: string): number {
    const t = new Date(`${dateStr}T12:00:00`).getTime();
    const now = Date.now();
    return Math.abs(t - now);
  }

  // closest-to-today at the top
  function sortSessionsByCloseness(
    list: TrainingSessionDTO[],
  ): TrainingSessionDTO[] {
    return list
      .slice()
      .sort(
        (a: any, b: any) =>
          dateClosenessSortKey(String(a.date)) -
          dateClosenessSortKey(String(b.date)),
      );
  }

  async function load() {
    loading = true;
    errorMsg = "";

    try {
      const programClient = new GetTrainingProgramEndpointClient();
      program = await programClient.get(programId);

      const sessionsClient = new GetTrainingSessionsEndpointClient();
      sessions = await sessionsClient.get(programId);

      // default display: closest date at the top
      sessions = sortSessionsByCloseness(sessions);
    } catch (e: any) {
      errorMsg = e?.message ?? "Failed to load program.";
    } finally {
      loading = false;
    }
  }

  function enterEdit() {
    if (!program) return;

    isEditing = true;

    draftTitle = program.title ?? "";
    draftStartDate = toIsoDateOnly(program.startDate);
    draftEndDate = toIsoDateOnly(program.endDate);

    // initialize per-session notes drafts
    sessionEdits = {};
    for (const s of sessions as any[]) {
      const id = s.trainingSessionID as string;
      if (id) sessionEdits[id] = { notes: s.notes ?? "" };
    }

    // reset reorder tracking
    pendingReorder = {};
  }

  function cancelEdit() {
    isEditing = false;
    load();
  }

  async function saveEdits() {
    if (!program) return;

    loading = true;
    errorMsg = "";

    try {
      // 1) update program
      const programClient = new UpdateTrainingProgramEndpointClient();

      const programReq: UpdateTrainingProgramRequest = {
        trainingProgramID: (program as any).trainingProgramID ?? programId,
        title: draftTitle,
        startDate: new Date(`${draftStartDate}T12:00:00`),
        endDate: new Date(`${draftEndDate}T12:00:00`),

        isCompleted: (program as any).isCompleted ?? false,
        tags: (program as any).tags ?? [],
      } as any;

      program = await programClient.put(programReq);

      // 2) update sessions: notes + swapped date changes
      const sessionClient = new UpdateTrainingSessionEndpointClient();

      for (const s of sessions as any[]) {
        const id = s.trainingSessionID as string;
        if (!id) continue;

        const newNotes = sessionEdits[id]?.notes ?? "";
        const oldNotes = s.notes ?? "";

        const dateChanged = pendingReorder[id] !== undefined;
        const notesChanged = newNotes !== oldNotes;

        if (!dateChanged && !notesChanged) continue;

        const dateStr = pendingReorder[id] ?? toIsoDateOnly(s.date);

        const req: UpdateTrainingSessionRequest = {
          trainingSessionID: id,
          trainingProgramID:
            s.trainingProgramID ??
            (program as any).trainingProgramID ??
            programId,
          date: new Date(`${dateStr}T12:00:00`),
          status: s.status,
          notes: newNotes,
          perceivedEffortRatings: s.perceivedEffortRatings ?? null,
        } as any;

        await sessionClient.put(req);
      }

      pendingReorder = {};
      isEditing = false;

      location.reload();
    } catch (e: any) {
      errorMsg = e?.message ?? "Failed to save changes.";
    } finally {
      loading = false;
    }
  }

  async function deleteSession(sessionId?: string) {
    if (!sessionId) return;
    if (!confirm("Delete this session? This cannot be undone.")) return;

    loading = true;
    errorMsg = "";
    try {
      const client = new DeleteTrainingSessionEndpointClient();
      await client.delete(sessionId);
      location.reload();
    } catch (e: any) {
      errorMsg = e?.message ?? "Failed to delete session.";
    } finally {
      loading = false;
    }
  }
  function updateNotes(id: string | undefined, value: string) {
    if (!id) return; // nothing to update
    sessionEdits[id].notes = value;
  }

  async function duplicateSession(sessionId?: string) {
    if (!sessionId) return;

    loading = true;
    errorMsg = "";
    try {
      const client = new DuplicateTrainingSessionEndpointClient();
      // most likely: post(trainingSessionID: string)
      await client.post(sessionId);
      location.reload();
    } catch (e: any) {
      errorMsg = e?.message ?? "Failed to duplicate session.";
    } finally {
      loading = false;
    }
  }

  async function deleteProgram() {
    const pid = (program as any)?.trainingProgramID ?? programId;
    if (!pid) return;
    if (!confirm("Delete this program? This will remove all sessions too."))
      return;

    loading = true;
    errorMsg = "";
    try {
      const client = new DeleteTrainingProgramEndpointClient();
      await client.delete(pid);
      goto("/training");
    } catch (e: any) {
      errorMsg = e?.message ?? "Failed to delete program.";
    } finally {
      loading = false;
    }
  }
  function toIsoDateOnly(raw: any): string {
    if (!raw) return "";

    // already "YYYY-MM-DD"
    if (typeof raw === "string") return raw.slice(0, 10);

    // JS Date
    if (raw instanceof Date) return raw.toISOString().slice(0, 10);

    // DateOnly-ish object: { year, month, day }
    if (typeof raw === "object" && raw.year && raw.month && raw.day) {
      const y = String(raw.year).padStart(4, "0");
      const m = String(raw.month).padStart(2, "0");
      const d = String(raw.day).padStart(2, "0");
      return `${y}-${m}-${d}`;
    }

    // fallback (avoid "[object Object]" poisoning)
    return "";
  }

  let dragFromId: string | null = null;
  let dragOverId: string | null = null;

  function onDragStartSession(e: DragEvent, s: TrainingSessionDTO) {
    const id = s.trainingSessionID;
    if (!id) return;

    dragFromId = id;
    dragOverId = null;

    const el = e.currentTarget as HTMLElement | null;
    if (el && e.dataTransfer) {
      const rect = el.getBoundingClientRect();

      // cursor hotspot = top-center of the card
      const x = Math.round(rect.width / 2);
      const y = 10; // a little below the top edge so it feels natural

      e.dataTransfer.setDragImage(el, x, y);
      e.dataTransfer.effectAllowed = "move";
    }
  }

  function onDragEnterSession(e: DragEvent, s: TrainingSessionDTO) {
    e.preventDefault();
    const id = s.trainingSessionID;
    if (!id) return;
    if (!dragFromId) return;
    if (id === dragFromId) return;

    dragOverId = id;
  }

  function onDragOverSession(e: DragEvent, s: TrainingSessionDTO) {
    // Needed so drop is allowed
    e.preventDefault();
    const id = s.trainingSessionID;
    if (!id) return;
    if (!dragFromId) return;
    if (id === dragFromId) return;

    dragOverId = id;
  }

  function onDragLeaveSession(e: DragEvent, s: TrainingSessionDTO) {
    const id = s.trainingSessionID;
    if (!id) return;
    if (dragOverId === id) dragOverId = null;
  }

  function onDragEndSession() {
    dragFromId = null;
    dragOverId = null;
  }

  /**
   * Swap dates between two sessions and record the change in pendingReorder.
   * Then re-sort UI based on closeness and re-render.
   */
  function swapSessionDatesById(aId: string, bId: string) {
    const a = sessions.find((x: any) => x.trainingSessionID === aId);
    const b = sessions.find((x: any) => x.trainingSessionID === bId);
    if (!a || !b) return;

    const aDate = toIsoDateOnly((a as any).date);
    const bDate = toIsoDateOnly((b as any).date);
    if (!aDate || !bDate) return;

    // swap in-memory
    (a as any).date = bDate;
    (b as any).date = aDate;

    // record for save()
    pendingReorder[aId] = bDate;
    pendingReorder[bId] = aDate;

    // reflect the "truth" of your UI sorting
    sessions = sortSessionsByCloseness(sessions);
  }

  function onDropOnSession(e: DragEvent, s: TrainingSessionDTO) {
    e.preventDefault();

    const toId = s.trainingSessionID;
    const fromId = dragFromId;

    if (!fromId || !toId) return;
    if (fromId === toId) return;

    // swap with the currently hovered/target session
    swapSessionDatesById(fromId, toId);

    // cleanup
    dragFromId = null;
    dragOverId = null;
  }

  onMount(load);
</script>

<svelte:head>
  <title>Program - Lionheart</title>
</svelte:head>

<div class={`min-h-full bg-base-200 ${isEditing ? "editing" : ""}`}>
  <div class="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
    <!-- Header -->
    <header class="mb-8">
      <button
        on:click={goBack}
        class="flex items-center gap-2 text-base-content/50 hover:text-base-content transition-colors mb-4"
      >
        <span>&larr;</span>
        <span class="text-sm font-mono uppercase tracking-widest"
          >Back to Training</span
        >
      </button>

      <div
        class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4"
      >
        <div>
          <h1
            class="text-5xl sm:text-6xl font-display font-black tracking-tightest text-base-content leading-none"
          >
            PROGRAM
          </h1>
          <p
            class="text-sm font-mono uppercase tracking-widest text-base-content/50 mt-3"
          >
            Program Overview
          </p>
        </div>

        <div class="flex items-center gap-2">
          {#if !isEditing}
            <button
              class="btn btn-primary px-5 rounded-xl"
              on:click={openAddSession}
            >
              Add Session
            </button>
            <button
              class="btn btn-outline px-5 rounded-xl"
              on:click={enterEdit}
            >
              Edit Program
            </button>
          {:else}
            <button
              class="btn btn-primary px-5 rounded-xl"
              on:click={saveEdits}
            >
              Done
            </button>
            <button class="btn btn-ghost px-5 rounded-xl" on:click={cancelEdit}>
              Cancel
            </button>
            <button
              class="btn btn-outline btn-error px-5 rounded-xl"
              on:click={deleteProgram}
            >
              Delete Program
            </button>
          {/if}
        </div>
      </div>
    </header>

    {#if loading}
      <div
        class="card bg-base-100 p-8 border border-base-content/10 rounded-2xl"
      >
        Loading program...
      </div>
    {:else if errorMsg}
      <div class="alert alert-error rounded-xl">
        <span>{errorMsg}</span>
      </div>
    {:else}
      <!-- Program Content -->
      <div
        class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-8 rounded-2xl"
      >
        <div class="flex items-start justify-between gap-4 mb-6">
          <!-- lock height so edit mode doesn't change card size -->
          <div class="min-h-[110px] w-full">
            {#if !isEditing}
              <h2 class="text-3xl font-display font-black leading-tight">
                {program?.title ?? "Program"}
              </h2>
              <div class="mt-3 text-sm text-base-content/60">
                {formatProgramRange(program?.startDate, program?.endDate)}
              </div>
            {:else}
              <input
                class="text-3xl font-display font-black leading-tight bg-transparent outline-none border-b border-base-content/30 focus:border-base-content/70 w-full pb-1"
                bind:value={draftTitle}
                placeholder="Program name"
              />

              <div
                class="mt-3 text-sm text-base-content/60 flex items-center gap-3"
              >
                <input
                  class="input input-sm input-bordered"
                  type="date"
                  bind:value={draftStartDate}
                />
                <span>→</span>
                <input
                  class="input input-sm input-bordered"
                  type="date"
                  bind:value={draftEndDate}
                />
              </div>
            {/if}
          </div>
        </div>

        <h3
          class="text-sm font-mono uppercase tracking-widest text-base-content/50 mb-3"
        >
          Sessions ({sessions.length})
        </h3>

        {#if sessions.length === 0}
          <div class="p-6 bg-base-200 rounded-xl">
            <div class="font-bold mb-2">No sessions yet</div>
            <div class="text-sm text-base-content/60">
              Click <span class="font-bold">Add Session</span> to create your first
              one.
            </div>
          </div>
        {:else if !isEditing}
          <div class="space-y-2">
            {#each sessions as s (s.trainingSessionID)}
              <button
                type="button"
                class="w-full text-left flex items-center justify-between p-4 rounded-xl bg-base-200 hover:bg-base-300 transition-colors"
                on:click={() => openSession(s.trainingSessionID)}
              >
                <div>
                  <div class="font-bold">{formatSessionDate(s.date)}</div>
                  <div class="text-xs font-mono text-base-content/50">
                    {s.status}
                  </div>
                </div>

                <div
                  class="text-xs text-base-content/50 flex items-center gap-3"
                >
                  <span>Movements: {s.movements?.length ?? 0}</span>
                  <span class="opacity-50">→</span>
                </div>
              </button>
            {/each}
          </div>
        {:else}
          <!-- EDIT MODE: swap-on-drop drag system + notes -->
          <div class="space-y-2">
            {#each sessions as s (s.trainingSessionID)}
              <div
                class={"p-4 rounded-xl bg-base-200 border border-base-content/10 wiggle " +
                  (dragOverId === s.trainingSessionID ? "swap-hover" : "") +
                  (dragFromId === s.trainingSessionID ? "swap-dragging" : "")}
                draggable="true"
                on:dragstart={(e) => onDragStartSession(e, s)}
                on:dragenter={(e) => onDragEnterSession(e, s)}
                on:dragover={(e) => onDragOverSession(e, s)}
                on:dragleave={(e) => onDragLeaveSession(e, s)}
                on:drop={(e) => onDropOnSession(e, s)}
                on:dragend={onDragEndSession}
              >
                <div class="relative">
                  <div class="flex items-start justify-between gap-4 pt-5">
                    <div>
                      <div class="font-bold">{formatSessionDate(s.date)}</div>
                      <div class="text-xs font-mono text-base-content/50 mt-1">
                        {s.status}
                      </div>
                    </div>

                    <div
                      class="text-xs text-base-content/50 flex items-center gap-3"
                    >
                      <span>Movements: {s.movements?.length ?? 0}</span>
                    </div>

                    <div class="flex items-center gap-2">
                      <button
                        type="button"
                        class="btn btn-xs btn-outline"
                        on:click={() => duplicateSession(s.trainingSessionID)}
                      >
                        Duplicate
                      </button>

                      <button
                        type="button"
                        class="btn btn-xs btn-outline btn-error"
                        on:click={() => deleteSession(s.trainingSessionID)}
                      >
                        Delete
                      </button>
                    </div>
                  </div>
                </div>

                <div class="mt-3">
                  <div
                    class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-2"
                  >
                    Notes
                  </div>

                  {#if s.trainingSessionID}
                    <textarea
                      class="textarea textarea-bordered w-full"
                      rows="2"
                      value={sessionEdits[s.trainingSessionID]?.notes ?? ""}
                      on:input={(e) =>
                        updateNotes(s.trainingSessionID, e.currentTarget.value)}
                      placeholder="Add notes for this session..."
                    />
                  {:else}
                    <textarea
                      class="textarea textarea-bordered w-full"
                      rows="2"
                      disabled
                    />
                  {/if}
                </div>
              </div>
            {/each}
          </div>
        {/if}
      </div>
    {/if}

    <CreateTrainingSessionModal
      bind:this={createSessionModal}
      trainingProgramId={programId}
    />
  </div>
</div>

<style>
  .wiggle {
    animation: none;
  }
  :global(.editing) .wiggle {
    animation: wiggle 0.18s infinite alternate ease-in-out;
    transform-origin: 50% 50%;
  }
  @keyframes wiggle {
    from {
      transform: rotate(-0.6deg);
    }
    to {
      transform: rotate(0.6deg);
    }
  }

  .dragpad {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 64px;
    height: 36px;
    border-radius: 999px;
    border: 1px solid rgba(255, 255, 255, 0.14);
    background: rgba(0, 0, 0, 0.35);
    font-size: 22px;
    line-height: 1;
    cursor: grab;
    user-select: none;
    backdrop-filter: blur(6px);
  }
  .dragpad:active {
    cursor: grabbing;
  }

  .swap-hover {
    outline: 2px solid rgba(255, 255, 255, 0.35);
    box-shadow: 0 0 0 4px rgba(255, 255, 255, 0.08);
  }

  .swap-dragging {
    opacity: 0.85;
    filter: saturate(1.1);
  }
</style>
