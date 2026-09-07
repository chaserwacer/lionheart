<script lang="ts">
  import { onDestroy } from "svelte";

  // Honest progress: the local model takes as long as it takes, so the elapsed time is shown
  // rather than a bar that pretends to know. The entry stays interruptible throughout.
  let {
    rawText,
    since,
    oninterrupt,
  }: { rawText: string; since: number; oninterrupt: () => void } = $props();

  let now = $state(Date.now());
  const elapsed = $derived(Math.max(0, Math.round((now - since) / 1000)));

  const ticker = setInterval(() => (now = Date.now()), 1000);

  onDestroy(() => clearInterval(ticker));
</script>

<div class="rounded-2xl border-2 border-base-content/10 bg-base-100 p-4 shadow-editorial">
  <div class="flex items-center gap-3">
    <span class="loading loading-ring loading-sm text-base-content/40"></span>
    <div class="min-w-0 flex-1">
      <p class="text-xs font-bold uppercase tracking-widest text-base-content/50">
        Reading it — {elapsed}s
      </p>
      <p class="mt-1 truncate text-sm text-base-content/70">{rawText}</p>
    </div>
    <button
      type="button"
      onclick={oninterrupt}
      class="btn btn-ghost btn-sm min-h-11 h-11 text-xs font-bold uppercase tracking-widest"
    >
      Stop
    </button>
  </div>

  <p class="mt-3 text-xs text-base-content/40">
    It is saved. You can leave this screen and it will still be here.
  </p>
</div>
