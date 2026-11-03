<script lang="ts">
  // The utterance is already on disk; this is the affordance that gets it to the server eventually.
  let {
    rawText,
    loggedAt,
    reason,
    retryable,
    onretry,
    ondiscard,
  }: {
    rawText: string;
    loggedAt: string;
    reason: string;
    retryable: boolean;
    onretry: () => void;
    ondiscard: () => void;
  } = $props();

  const when = $derived(
    new Date(loggedAt).toLocaleString(undefined, {
      month: "short",
      day: "numeric",
      hour: "numeric",
      minute: "2-digit",
    }),
  );
</script>

<div class="rounded-2xl border-2 border-base-content/20 bg-base-100 p-4 shadow-editorial">
  <p class="text-xs font-bold uppercase tracking-widest text-base-content/50">
    Saved, not sent — {when}
  </p>

  <p class="mt-2 text-sm text-base-content/80">“{rawText}”</p>
  <p class="mt-2 text-xs text-error">{reason}</p>

  <div class="mt-3 flex gap-2">
    {#if retryable}
      <button
        type="button"
        onclick={onretry}
        class="btn btn-neutral btn-sm min-h-11 h-11 rounded-xl px-5 text-xs font-bold uppercase tracking-widest"
      >
        Retry
      </button>
    {/if}
    <button
      type="button"
      onclick={ondiscard}
      class="btn btn-ghost btn-sm min-h-11 h-11 text-xs font-bold uppercase tracking-widest"
    >
      Discard
    </button>
  </div>
</div>
