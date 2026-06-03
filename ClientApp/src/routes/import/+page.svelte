<script lang="ts">
  import { onDestroy } from 'svelte';
  import {
    currentStep,
    draftSessions,
    parseWarnings,
    allResolved,
    isLoading,
    errorMsg,
    resetIngestion,
  } from '$lib/stores/ingestionStore';

  import ImportStepper from '$lib/components/ingestion/ImportStepper.svelte';
  import IngestionInput from '$lib/components/ingestion/IngestionInput.svelte';
  import DraftSessionCard from '$lib/components/ingestion/DraftSessionCard.svelte';
  import CommitSummary from '$lib/components/ingestion/CommitSummary.svelte';

  onDestroy(() => {
    resetIngestion();
  });
</script>

<div class="max-w-4xl mx-auto px-4 py-8">
  <!-- Header -->
  <div class="mb-6">
    <h1 class="text-3xl font-display font-bold tracking-tightest">Import Workout</h1>
    <p class="text-base-content/50 mt-1">Paste or drop workout text to create training sessions.</p>
  </div>

  <!-- Stepper -->
  <ImportStepper />

  <!-- Step: Input -->
  {#if $currentStep === 'input'}
    <IngestionInput />
  {/if}

  <!-- Step: Review & Resolve -->
  {#if $currentStep === 'review'}
    <div class="space-y-4">
      <!-- Warnings -->
      {#if $parseWarnings.length > 0}
        <div class="alert alert-warning rounded-xl">
          <div>
            <p class="font-medium">Parse warnings:</p>
            <ul class="list-disc list-inside text-sm mt-1">
              {#each $parseWarnings as warning}
                <li>{warning}</li>
              {/each}
            </ul>
          </div>
        </div>
      {/if}

      <!-- Resolution status -->
      {#if !$allResolved}
        <div class="alert alert-info rounded-xl">
          <span>Some entities need to be resolved before committing. Use the dropdowns below to map or create them.</span>
        </div>
      {/if}

      <!-- Draft session cards -->
      {#each $draftSessions as session, idx (session.draftId)}
        <DraftSessionCard {session} sessionIdx={idx} />
      {/each}

      {#if $draftSessions.length === 0}
        <div class="text-center py-12 text-base-content/40">
          <p>No sessions were parsed from the input.</p>
          <button class="btn btn-outline rounded-xl mt-4" on:click={() => currentStep.set('input')}>
            Go Back
          </button>
        </div>
      {/if}

      <!-- Error message -->
      {#if $errorMsg}
        <div class="alert alert-error rounded-xl">
          <span>{$errorMsg}</span>
        </div>
      {/if}

      <!-- Navigation -->
      {#if $draftSessions.length > 0}
        <div class="flex justify-between pt-4">
          <button
            class="btn btn-outline rounded-xl"
            on:click={() => currentStep.set('input')}
          >
            Back to Input
          </button>
          <button
            class="btn btn-primary rounded-xl"
            disabled={!$allResolved}
            on:click={() => currentStep.set('commit')}
          >
            Continue to Commit
          </button>
        </div>
      {/if}
    </div>
  {/if}

  <!-- Step: Commit -->
  {#if $currentStep === 'commit' || $currentStep === 'done'}
    <CommitSummary />
  {/if}
</div>
