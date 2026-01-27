<script lang="ts">
  import { page } from "$app/stores";
  import { goto } from "$app/navigation";
  import { onMount } from "svelte";

  import {
    GetTrainingSessionEndpointClient,
    UpdateTrainingSessionEndpointClient,
    UpdateTrainingSessionRequest,
    DeleteMovementEndpointClient,
    UpdateMovementOrderEndpointClient,
    UpdateMovementOrderRequest,
    TrainingSessionDTO,
    TrainingSessionStatus,
    MovementDTO,
  } from "$lib/api/ApiClient";

  $: programId = $page.params.programId;
  $: sessionId = $page.params.sessionId;

  $: statusText = sessionStatusText(session);
  $: notesText = sessionNotesText(session);

  const SESSION_STATUSES = [
  TrainingSessionStatus._0,
  TrainingSessionStatus._1,
  TrainingSessionStatus._2,
  TrainingSessionStatus._3,
  TrainingSessionStatus._4
];

  let session: TrainingSessionDTO | null = null;
  let loading = true;
  let errorMsg = "";

  let isEditing = false;

  // drafts
  let draftDate = ""; // YYYY-MM-DD
  let draftStatus: TrainingSessionStatus = TrainingSessionStatus._0;
  let draftNotes = "";

  // movement reorder tracking
  let movements: MovementDTO[] = []; // canonical list for UI + reorder
  let pendingOrderIds: string[] | null = null;

  // drag state (swap-on-drop like your program page)
  let dragFromId: string | null = null;
  let dragOverId: string | null = null;

  function goBack() {
    goto("/training");
  }

  function toIsoDateOnly(raw: any): string {
    if (!raw) return "";

    if (typeof raw === "string") return raw.slice(0, 10);
    if (raw instanceof Date) return raw.toISOString().slice(0, 10);

    if (typeof raw === "object" && raw.year && raw.month && raw.day) {
      const y = String(raw.year).padStart(4, "0");
      const m = String(raw.month).padStart(2, "0");
      const d = String(raw.day).padStart(2, "0");
      return `${y}-${m}-${d}`;
    }

    return "";
  }

  function formatSessionDate(raw: any): string {
    const d = raw instanceof Date ? raw : new Date(`${toIsoDateOnly(raw)}T12:00:00`);
    if (Number.isNaN(d.getTime())) return String(raw);

    const weekday = d.toLocaleDateString(undefined, { weekday: "long" });
    const day = String(d.getDate()).padStart(2, "0");
    const month = String(d.getMonth() + 1).padStart(2, "0");
    return `${weekday} ${day}/${month}`;
  }
  function statusLabel(s: TrainingSessionStatus): string {
  switch (s) {
    case TrainingSessionStatus._0:
      return "Planned";
    case TrainingSessionStatus._1:
      return "Active";
    case TrainingSessionStatus._2:
      return "Completed";
    case TrainingSessionStatus._3:
      return "Skipped";
    case TrainingSessionStatus._4:
      return "AI Modified";
    default:
      return "Unknown";
  }
}

  async function load() {
    loading = true;
    errorMsg = "";
    session = null;
    isEditing = false;
    pendingOrderIds = null;

    try {
      const client = new GetTrainingSessionEndpointClient();
      session = await client.get(sessionId, programId);

      movements = (session.movements ?? []) as any[];
    } catch (e: any) {
      errorMsg = e?.body?.title || e?.body?.detail || e?.message || "Failed to load session.";
    } finally {
      loading = false;
    }
  }

  function enterEdit() {
    if (!session) return;

    isEditing = true;

    draftDate = toIsoDateOnly(session.date);
    draftStatus = session?.status ?? TrainingSessionStatus._0;
    draftNotes = (session as any).notes ?? "";

    movements = (session.movements ?? []) as any[];
    pendingOrderIds = null;
  }

  function cancelEdit() {
    // just reload to revert everything (matches your preference for predictability)
    load();
  }

  async function saveEdits() {
    if (!session) return;

    loading = true;
    errorMsg = "";

    try {
      // 1) update session basics
      const sessionClient = new UpdateTrainingSessionEndpointClient();

      const req: UpdateTrainingSessionRequest = {
        trainingSessionID: (session as any).trainingSessionID ?? sessionId,
        trainingProgramID: (session as any).trainingProgramID ?? programId,
        date: new Date(`${draftDate}T12:00:00`),
        status: draftStatus,
        notes: draftNotes,
        perceivedEffortRatings: (session as any).perceivedEffortRatings ?? null
      } as any;

      session = await sessionClient.put(req);

      // 2) persist movement order if changed
      if (pendingOrderIds && pendingOrderIds.length > 0) {
        const orderClient = new UpdateMovementOrderEndpointClient();

        // NOTE: field names depend on your generated client.
        // Using `as any` keeps this resilient while you confirm the DTO shape.
        const orderReq: UpdateMovementOrderRequest = {
          trainingSessionID: (session as any).trainingSessionID ?? sessionId,
          movementIdsInOrder: pendingOrderIds
        } as any;

        await orderClient.put(orderReq as any);
      }

      // reset + hard refresh (your standard pattern)
      isEditing = false;
      pendingOrderIds = null;
      location.reload();
    } catch (e: any) {
      errorMsg = e?.body?.title || e?.body?.detail || e?.message || "Failed to save changes.";
    } finally {
      loading = false;
    }
  }

  async function deleteMovement(movementId?: string) {
    if (!movementId) return;
    if (!confirm("Remove this movement? This cannot be undone.")) return;

    loading = true;
    errorMsg = "";

    try {
      const client = new DeleteMovementEndpointClient();
      await client.delete(movementId as any);

      // optimistic remove from UI in edit mode
      movements = movements.filter((m: any) => (m.movementID ?? m.movementId) !== movementId);
      pendingOrderIds = movements.map((m: any) => String(m.movementID ?? m.movementId));
    } catch (e: any) {
      errorMsg = e?.body?.title || e?.body?.detail || e?.message || "Failed to delete movement.";
    } finally {
      loading = false;
    }
  }

  function movementIdOf(m: any): string {
    return String(m?.movementID ?? m?.movementId ?? "");
  }

  function onDragStartMovement(e: DragEvent, m: MovementDTO) {
    const id = movementIdOf(m);
    if (!id) return;

    dragFromId = id;
    dragOverId = null;

    const el = e.currentTarget as HTMLElement | null;
    if (el && e.dataTransfer) {
      const rect = el.getBoundingClientRect();
      e.dataTransfer.setDragImage(el, Math.round(rect.width / 2), 10);
      e.dataTransfer.effectAllowed = "move";
    }
  }

  function onDragEnterMovement(e: DragEvent, m: MovementDTO) {
    e.preventDefault();
    const id = movementIdOf(m);
    if (!id || !dragFromId || id === dragFromId) return;
    dragOverId = id;
  }

  function onDragOverMovement(e: DragEvent, m: MovementDTO) {
    e.preventDefault();
    const id = movementIdOf(m);
    if (!id || !dragFromId || id === dragFromId) return;
    dragOverId = id;
  }

  function onDragLeaveMovement(_e: DragEvent, m: MovementDTO) {
    const id = movementIdOf(m);
    if (dragOverId === id) dragOverId = null;
  }

  function onDragEndMovement() {
    dragFromId = null;
    dragOverId = null;
  }

  function swapMovementsById(aId: string, bId: string) {
    const aIndex = movements.findIndex((x: any) => movementIdOf(x) === aId);
    const bIndex = movements.findIndex((x: any) => movementIdOf(x) === bId);
    if (aIndex < 0 || bIndex < 0) return;

    const copy = movements.slice();
    const tmp = copy[aIndex];
    copy[aIndex] = copy[bIndex];
    copy[bIndex] = tmp;

    movements = copy;
    pendingOrderIds = movements.map((m: any) => movementIdOf(m));
  }

  function onDropOnMovement(e: DragEvent, m: MovementDTO) {
    e.preventDefault();

    const toId = movementIdOf(m);
    const fromId = dragFromId;

    if (!fromId || !toId || fromId === toId) return;

    swapMovementsById(fromId, toId);

    dragFromId = null;
    dragOverId = null;
  }

  // ---- template-safe helpers (NO "as" in markup) ----
function sessionStatusText(s: TrainingSessionDTO | null): string {
  if (!s) return "";
  // cast lives in script, not in template
  return String((s as any).status ?? "");
}

function sessionNotesText(s: TrainingSessionDTO | null): string {
  if (!s) return "";
  return String((s as any).notes ?? "");
}

function movementName(m: MovementDTO): string {
  return String((m as any)?.movementData?.movementBase?.name ?? "Movement");
}

function movementSetCount(m: MovementDTO): number {
  const lift = (m as any)?.liftSets?.length ?? 0;
  const dt = (m as any)?.distanceTimeSets?.length ?? 0;
  return lift + dt;
}


  onMount(load);
</script>

<svelte:head>
  <title>Session - Lionheart</title>
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
        <span class="text-sm font-mono uppercase tracking-widest">Back to Training</span>
      </button>

      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 class="text-5xl sm:text-6xl font-display font-black tracking-tightest text-base-content leading-none">
            SESSION
          </h1>
          <p class="text-sm font-mono uppercase tracking-widest text-base-content/50 mt-3">
            {#if session}
              {formatSessionDate(session.date)}
            {:else}
              Session Details
            {/if}
          </p>
        </div>

        <div class="flex items-center gap-2">
          {#if !isEditing}
            <button class="btn btn-primary px-5 rounded-xl" disabled={!session}>
              Start Session
            </button>
            <button class="btn btn-outline px-5 rounded-xl" disabled={!session} on:click={enterEdit}>
              Edit
            </button>
          {:else}
            <button class="btn btn-primary px-5 rounded-xl" on:click={saveEdits} disabled={loading}>
              Done
            </button>
            <button class="btn btn-ghost px-5 rounded-xl" on:click={cancelEdit} disabled={loading}>
              Cancel
            </button>
          {/if}
        </div>
      </div>
    </header>

    <!-- Body -->
    {#if loading}
      <div class="card bg-base-100 p-8 border border-base-content/10 rounded-2xl">
        Loading session...
      </div>

    {:else if errorMsg}
      <div class="alert alert-error rounded-xl">
        <span>{errorMsg}</span>
      </div>

    {:else if session}
      <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-8 rounded-2xl">
        <div class="flex items-start justify-between gap-4 mb-6">
          <div class="min-h-[110px] w-full">
            {#if !isEditing}
              <h2 class="text-3xl font-display font-black">
                {formatSessionDate(session.date)}
              </h2>

              <p class="text-sm text-base-content/60 mt-1">
                Status: <span class="font-mono">{statusText}</span>
              </p>

              {#if notesText}
                <p class="text-sm text-base-content/60 mt-3 whitespace-pre-wrap">
                  {notesText}
                </p>
              {/if}
            {:else}
              <div class="flex flex-wrap items-center gap-3">
                <input class="input input-sm input-bordered" type="date" bind:value={draftDate} />
                  <select
                    class="select select-sm select-bordered"
                    bind:value={draftStatus}
                  >
                    {#each SESSION_STATUSES as s}
                      <option value={s}>{statusLabel(s)}</option>
                    {/each}
                  </select>
              </div>
              <div class="mt-4">
                <div class="text-xs font-mono uppercase tracking-widest text-base-content/50 mb-2">
                  Notes
                </div>
                <textarea
                  class="textarea textarea-bordered w-full"
                  rows="3"
                  bind:value={draftNotes}
                  placeholder="Add notes for this session..."
                />
              </div>
            {/if}
          </div>

          <div class="text-sm text-base-content/60">
            Movements: {movements?.length ?? 0}
          </div>
        </div>

        {#if (movements?.length ?? 0) === 0}
          <div class="p-6 bg-base-200 rounded-xl">
            <div class="font-bold mb-2">No movements yet</div>
            <div class="text-sm text-base-content/60">
              This session is empty right now. Add movements next.
            </div>
          </div>
        {:else if !isEditing}
          <div class="space-y-2">
            {#each movements as m (movementIdOf(m))}
              <div class="p-4 rounded-xl bg-base-200">
                <div class="font-bold">
                  {movementName(m)}
                </div>

                <div class="text-xs font-mono text-base-content/50">
                  Sets: {movementSetCount(m)}
                </div>
              </div>
            {/each}
          </div>
        {:else}
          <!-- EDIT MODE: drag swap + delete -->
          <div class="space-y-2">
            {#each movements as m (movementIdOf(m))}
              <div
                class={"p-4 rounded-xl bg-base-200 border border-base-content/10 wiggle " +
                  (dragOverId === movementIdOf(m) ? "swap-hover" : "") +
                  (dragFromId === movementIdOf(m) ? "swap-dragging" : "")}
                draggable="true"
                on:dragstart={(e) => onDragStartMovement(e, m)}
                on:dragenter={(e) => onDragEnterMovement(e, m)}
                on:dragover={(e) => onDragOverMovement(e, m)}
                on:dragleave={(e) => onDragLeaveMovement(e, m)}
                on:drop={(e) => onDropOnMovement(e, m)}
                on:dragend={onDragEndMovement}
              >
                <div class="flex items-start justify-between gap-4">
                  <div>
                    <div class="font-bold">
                      {movementName(m)}
                    </div>
                    <div class="text-xs font-mono text-base-content/50 mt-1">
                      Sets: {movementSetCount(m)}
                    </div>
                  </div>

                  <div class="flex items-center gap-2">
                    <button
                      type="button"
                      class="btn btn-xs btn-outline btn-error"
                      on:click={() => deleteMovement(movementIdOf(m))}
                    >
                      Delete
                    </button>
                  </div>
                </div>
              </div>
            {/each}
          </div>

          {#if pendingOrderIds}
            <div class="mt-3 text-xs text-base-content/50">
              Order changed — will save when you hit <span class="font-bold">Done</span>.
            </div>
          {/if}
        {/if}
      </div>

    {:else}
      <div class="card bg-base-100 p-8 border border-base-content/10 rounded-2xl">
        Session not found.
      </div>
    {/if}
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
    from { transform: rotate(-0.6deg); }
    to { transform: rotate(0.6deg); }
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
