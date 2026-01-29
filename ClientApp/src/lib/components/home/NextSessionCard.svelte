<script lang="ts">
  import { goto } from "$app/navigation";
  import type { TrainingSessionDTO, MovementDTO, TrainingSessionStatus } from "$lib/api/ApiClient";
  import { GetMovementsEndpointClient } from "$lib/api/ApiClient";
  import { onMount } from "svelte";

  export let session: TrainingSessionDTO | null = null;

  // Optional: let pages pass baseUrl if needed (you use "" today)
  export let baseUrl = "";

  // If the session DTO doesn't include movements, we'll fetch them.
  let movements: MovementDTO[] = [];
  let loadingMoves = false;

  onMount(async () => {
    await ensureMovementsLoaded();
  });

  $: if (session?.trainingSessionID) {
    // When session changes, refresh local movements
    ensureMovementsLoaded();
  }

  async function ensureMovementsLoaded() {
    if (!session?.trainingSessionID) return;

    // If movements already present, just use them.
    if (session.movements && session.movements.length) {
      movements = session.movements;
      return;
    }

    // Otherwise fetch them.
    try {
      loadingMoves = true;
      const client = new GetMovementsEndpointClient(baseUrl);
      const result = await client.get(session.trainingSessionID);
      movements = result ?? [];
    } catch (e) {
      // Don't explode the UI — just show "0" moves.
      movements = [];
      console.error("NextSessionCard failed to load movements", e);
    } finally {
      loadingMoves = false;
    }
  }

  function openSession() {
    if (!session?.trainingSessionID) return;
    goto(`/training/session/${session.trainingSessionID}`);
  }

  function formatDate(date: any): string {
    if (!date) return "Not scheduled";
    const d = new Date(date.toString());
    const today = new Date();
    const tomorrow = new Date(today);
    tomorrow.setDate(tomorrow.getDate() + 1);

    const sameDay = (a: Date, b: Date) => a.toDateString() === b.toDateString();
    if (sameDay(d, today)) return "Today";
    if (sameDay(d, tomorrow)) return "Tomorrow";

    return d.toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric" });
  }

  function statusText(status: TrainingSessionStatus | undefined) {
    // Adjust these labels if your enum meanings differ
    if (status === undefined) return "Planned";
    const n = Number(status);
    if (n === 2) return "Completed";
    if (n === 1) return "Skipped";
    return "Planned";
  }

  function statusBadge(status: TrainingSessionStatus | undefined) {
    if (status === undefined) return "badge-ghost";
    const n = Number(status);
    if (n === 2) return "badge-success";
    if (n === 1) return "badge-warning";
    return "badge-ghost";
  }

  function sessionTitle(s: TrainingSessionDTO | null) {
    if (!s) return "Session";
    if (s.notes?.trim()) return s.notes.trim().slice(0, 30);
    return "Training Session";
  }

  $: movementCount = movements?.length ?? session?.movements?.length ?? 0;
</script>

<button
  on:click={openSession}
  class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
         cursor-pointer p-6 text-left w-full h-full
         border-2 border-base-content/10 hover:border-base-content/30
         {!session ? 'opacity-60 cursor-not-allowed' : ''}"
  disabled={!session}
>
  <div class="flex items-start justify-between mb-4">
    <div class="flex flex-col gap-1">
      <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Next</span>
      <h3 class="text-2xl font-display font-black tracking-tight">Session</h3>
    </div>

    {#if session}
      <span class="badge {statusBadge(session.status)} badge-sm">
        {statusText(session.status)}
      </span>
    {/if}
  </div>

  {#if session}
    <div class="flex items-start justify-between gap-3">
      <div class="min-w-0">
        <p class="text-xl font-bold uppercase tracking-wide truncate">{sessionTitle(session)}</p>
        <p class="text-sm font-mono uppercase tracking-widest text-base-content/50 mt-2">
          {formatDate(session.date)}
        </p>
      </div>

      <span class="text-base-content/30 text-xl shrink-0">&rarr;</span>
    </div>

    <div class="mt-6 pt-4 border-t-2 border-base-content/10">
      <div class="flex items-baseline gap-2">
        <span class="text-4xl font-display font-black">{loadingMoves ? "…" : movementCount}</span>
        <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">
          Movements Planned
        </span>
      </div>

      {#if movements?.length}
        <div class="mt-4 space-y-2">
          {#each movements.slice(0, 4) as m}
            <div class="text-sm text-base-content/70 truncate">
              • {m.movementData?.movementBase?.name ?? "Unknown"}
            </div>
          {/each}
          {#if movements.length > 4}
            <div class="text-xs text-base-content/40">
              +{movements.length - 4} more
            </div>
          {/if}
        </div>
      {/if}
    </div>
  {:else}
    <div class="py-4">
      <p class="text-base-content/50">No upcoming session</p>
      <p class="text-xs text-base-content/30 mt-1">Create a new session</p>
    </div>
  {/if}
</button>
