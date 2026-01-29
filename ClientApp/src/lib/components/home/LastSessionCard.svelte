<script lang="ts">
  import { goto } from "$app/navigation";
  import type { TrainingSessionDTO, MovementDTO, LiftSetEntryDTO, TrainingSessionStatus } from "$lib/api/ApiClient";
  import { GetMovementsEndpointClient } from "$lib/api/ApiClient";
  import { onMount } from "svelte";

  export let session: TrainingSessionDTO | null = null;
  export let baseUrl = "";

  let movements: MovementDTO[] = [];
  let loadingMoves = false;

  onMount(async () => {
    await ensureMovementsLoaded();
  });

  $: if (session?.trainingSessionID) {
    ensureMovementsLoaded();
  }

  async function ensureMovementsLoaded() {
    if (!session?.trainingSessionID) return;

    if (session.movements && session.movements.length) {
      movements = session.movements;
      return;
    }

    try {
      loadingMoves = true;
      const client = new GetMovementsEndpointClient(baseUrl);
      const result = await client.get(session.trainingSessionID);
      movements = result ?? [];
    } catch (e) {
      movements = [];
      console.error("LastSessionCard failed to load movements", e);
    } finally {
      loadingMoves = false;
    }
  }

  function openSession() {
    if (!session?.trainingSessionID) return;
    goto(`/training/session/${session.trainingSessionID}`);
  }

  function formatDate(date: any): string {
    if (!date) return "Unknown";
    const d = new Date(date.toString());
    const today = new Date();
    const diffMs = today.getTime() - d.getTime();
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

    if (diffDays === 0) return "Today";
    if (diffDays === 1) return "Yesterday";
    if (diffDays > 1 && diffDays < 7) return `${diffDays} days ago`;
    return d.toLocaleDateString("en-US", { month: "short", day: "numeric" });
  }

  function statusText(status: TrainingSessionStatus | undefined) {
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

  $: totalSets =
    movements?.reduce((sum, m) => {
      const lift = m.liftSets?.length ?? 0;
      const dt = m.distanceTimeSets?.length ?? 0;
      return sum + lift + dt;
    }, 0) ?? 0;

  $: totalVolume =
    movements?.reduce((sum, m) => {
      const liftVol =
        m.liftSets?.reduce(
          (s: number, e: LiftSetEntryDTO) => s + (e.actualWeight ?? 0) * (e.actualReps ?? 0),
          0,
        ) ?? 0;
      return sum + liftVol;
    }, 0) ?? 0;

  function formatVolume(vol: number) {
    if (!vol) return "0";
    if (vol > 1000) return `${(vol / 1000).toFixed(1)}k`;
    return `${vol}`;
  }
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
      <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Last</span>
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

    <div class="mt-6 pt-4 border-t-2 border-base-content/10 grid grid-cols-3 gap-4">
      <div class="flex flex-col gap-1">
        <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Moves</span>
        <span class="text-2xl font-display font-black">{loadingMoves ? "…" : movementCount}</span>
      </div>
      <div class="flex flex-col gap-1">
        <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Sets</span>
        <span class="text-2xl font-display font-black">{totalSets}</span>
      </div>
      <div class="flex flex-col gap-1">
        <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Volume</span>
        <span class="text-2xl font-display font-black">{formatVolume(totalVolume)}</span>
      </div>
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
  {:else}
    <div class="py-4">
      <p class="text-base-content/50">No completed sessions</p>
      <p class="text-xs text-base-content/30 mt-1">Complete your first session</p>
    </div>
  {/if}
</button>
