<script lang="ts">
  import { onMount } from "svelte";
  import { GetMovementDatasEndpointClient } from "$lib/api/ApiClient";
  import { quickLog } from "$lib/stores/quickLog.svelte";
  import UtteranceComposer from "$lib/components/quicklog/UtteranceComposer.svelte";
  import WorkingEntry from "$lib/components/quicklog/WorkingEntry.svelte";
  import ConfirmationEntry from "$lib/components/quicklog/ConfirmationEntry.svelte";
  import FailedEntry from "$lib/components/quicklog/FailedEntry.svelte";
  import ReceiptCard from "$lib/components/quicklog/ReceiptCard.svelte";

  let online = $state(true);

  onMount(() => {
    quickLog.load();
    online = navigator.onLine;
    loadMovementNames();
  });

  async function loadMovementNames() {
    try {
      const movements = await new GetMovementDatasEndpointClient("").get();
      const names: Record<string, string> = {};
      for (const movement of movements ?? []) {
        if (!movement.movementDataID) continue;
        const parts = [
          movement.movementModifier?.name,
          movement.equipment?.name,
          movement.movementBase?.name,
        ].filter((part): part is string => Boolean(part));
        names[movement.movementDataID] = parts.join(" ") || "Movement";
      }
      quickLog.movementNames = names;
    } catch {
      // Names are a nicety on the receipt; the log itself does not depend on them.
    }
  }
</script>

<svelte:head>
  <title>Log - Lionheart</title>
</svelte:head>

<svelte:window
  onoffline={() => (online = false)}
  ononline={() => (online = true)}
/>

<div class="min-h-full bg-base-200">
  <div class="mx-auto flex max-w-2xl flex-col gap-6 px-4 py-6 sm:px-6 sm:py-10">
    <header>
      <h1
        class="font-display text-5xl font-black leading-none tracking-tightest text-base-content"
      >
        LOG
      </h1>
      <p class="mt-2 text-sm font-mono uppercase tracking-widest text-base-content/50">
        Say what you did
      </p>
    </header>

    {#if !online}
      <p
        class="rounded-xl border-2 border-warning/40 bg-warning/10 px-4 py-3 text-xs font-bold uppercase tracking-widest text-warning"
      >
        Offline — entries are saved here and retried when you are back
      </p>
    {/if}

    <UtteranceComposer
      bind:value={quickLog.utterance}
      onsubmit={() => quickLog.submit()}
    />

    {#each quickLog.entries as entry (entry.id)}
      {#if entry.state.kind === "interpreting"}
        <WorkingEntry
          rawText={entry.rawText}
          since={entry.state.since}
          oninterrupt={() => quickLog.interrupt(entry.id)}
        />
      {:else if entry.state.kind === "awaiting"}
        <ConfirmationEntry
          references={entry.state.references}
          choices={entry.state.choices}
          onchoose={(reference, chosenEntityId) =>
            quickLog.choose(entry.id, reference, chosenEntityId)}
        />
      {:else}
        <FailedEntry
          rawText={entry.rawText}
          loggedAt={entry.loggedAt}
          reason={entry.state.reason}
          retryable={entry.state.retryable}
          onretry={() => quickLog.retry(entry.id)}
          ondiscard={() => quickLog.discard(entry.id)}
        />
      {/if}
    {/each}

    {#each quickLog.receipts as receipt (receipt.id)}
      <ReceiptCard
        {receipt}
        movementNames={quickLog.movementNames}
        onundo={() => quickLog.undo(receipt.id)}
        ondismiss={() => quickLog.dismissReceipt(receipt.id)}
      />
    {/each}

    {#if quickLog.entries.length === 0 && quickLog.receipts.length === 0}
      <p class="text-sm leading-relaxed text-base-content/40">
        Write it the way you would say it. Movements, weights, how it felt — all in one go. Anything
        the parser cannot place, it will ask about, and only about that.
      </p>
    {/if}
  </div>
</div>
