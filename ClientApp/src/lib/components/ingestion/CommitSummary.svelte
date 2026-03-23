<script lang="ts">
  import {
    commitResult,
    sessionCount,
    movementCount,
    newEntityCount,
    commitDraft,
    isLoading,
    errorMsg,
    allResolved,
    resetIngestion,
    currentStep,
  } from '$lib/stores/ingestionStore';

  $: isDone = $currentStep === 'done';
</script>

{#if isDone && $commitResult}
  <!-- Post-commit report -->
  <div class="space-y-4">
    {#if $commitResult.errors.length === 0}
      <div class="alert alert-success rounded-xl">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        <span>Import completed successfully!</span>
      </div>
    {:else}
      <div class="alert alert-warning rounded-xl">
        <span>Import completed with some errors.</span>
      </div>
    {/if}

    <!-- Created sessions -->
    {#if $commitResult.createdSessionIDs.length > 0}
      <div>
        <h4 class="font-medium mb-2">Created Sessions ({$commitResult.createdSessionIDs.length})</h4>
        <div class="flex flex-wrap gap-2">
          {#each $commitResult.createdSessionIDs as id}
            <a
              href="/training/session/{id}"
              class="btn btn-sm btn-outline rounded-lg"
            >
              View Session
            </a>
          {/each}
        </div>
      </div>
    {/if}

    <!-- Created dependencies -->
    {#if $commitResult.createdDependencies.length > 0}
      <div>
        <h4 class="font-medium mb-2">Created Dependencies</h4>
        <ul class="list-disc list-inside text-sm text-base-content/70">
          {#each $commitResult.createdDependencies as dep}
            <li>{dep}</li>
          {/each}
        </ul>
      </div>
    {/if}

    <!-- Errors -->
    {#if $commitResult.errors.length > 0}
      <div>
        <h4 class="font-medium mb-2 text-error">Errors</h4>
        <ul class="list-disc list-inside text-sm text-error/80">
          {#each $commitResult.errors as error}
            <li>{error}</li>
          {/each}
        </ul>
      </div>
    {/if}

    <div class="flex justify-center pt-4">
      <button class="btn btn-primary rounded-xl" on:click={resetIngestion}>
        Import Another
      </button>
    </div>
  </div>
{:else}
  <!-- Pre-commit summary -->
  <div class="space-y-4">
    <div class="stats stats-horizontal shadow w-full">
      <div class="stat">
        <div class="stat-title">Sessions</div>
        <div class="stat-value text-2xl">{$sessionCount}</div>
      </div>
      <div class="stat">
        <div class="stat-title">Movements</div>
        <div class="stat-value text-2xl">{$movementCount}</div>
      </div>
      <div class="stat">
        <div class="stat-title">New Entities</div>
        <div class="stat-value text-2xl">{$newEntityCount}</div>
      </div>
    </div>

    {#if !$allResolved}
      <div class="alert alert-warning rounded-xl">
        <span>Some entity references are not yet resolved. Please go back and resolve them before committing.</span>
      </div>
    {/if}

    {#if $errorMsg}
      <div class="alert alert-error rounded-xl">
        <span>{$errorMsg}</span>
      </div>
    {/if}

    <div class="flex justify-between">
      <button
        class="btn btn-outline rounded-xl"
        on:click={() => currentStep.set('review')}
      >
        Back to Review
      </button>
      <button
        class="btn btn-primary rounded-xl"
        disabled={$isLoading || !$allResolved || $sessionCount === 0}
        on:click={commitDraft}
      >
        {#if $isLoading}
          <span class="loading loading-spinner loading-sm"></span>
          Committing...
        {:else}
          Commit {$sessionCount} Session{$sessionCount !== 1 ? 's' : ''}
        {/if}
      </button>
    </div>
  </div>
{/if}
