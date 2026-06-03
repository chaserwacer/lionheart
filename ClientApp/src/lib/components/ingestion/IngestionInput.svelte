<script lang="ts">
  import { rawText, parseInput, isLoading, errorMsg } from '$lib/stores/ingestionStore';

  let dragOver = false;

  function handleDrop(e: DragEvent) {
    e.preventDefault();
    dragOver = false;
    const files = e.dataTransfer?.files;
    if (files && files.length > 0) {
      const file = files[0];
      if (file.type === 'text/plain' || file.name.endsWith('.txt') || file.name.endsWith('.csv')) {
        const reader = new FileReader();
        reader.onload = () => {
          rawText.set(reader.result as string);
        };
        reader.readAsText(file);
      }
    }
  }

  function handleDragOver(e: DragEvent) {
    e.preventDefault();
    dragOver = true;
  }

  function handleDragLeave() {
    dragOver = false;
  }

  function handleFileInput(e: Event) {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = () => {
        rawText.set(reader.result as string);
      };
      reader.readAsText(file);
    }
  }

  let fileInput: HTMLInputElement;
</script>

<div class="space-y-4">
  <!-- Drop zone / textarea -->
  <div
    class="relative"
    on:drop={handleDrop}
    on:dragover={handleDragOver}
    on:dragleave={handleDragLeave}
    role="region"
  >
    {#if !$rawText}
      <div
        class="border-2 border-dashed rounded-2xl p-12 text-center transition-all cursor-pointer
               {dragOver ? 'border-primary bg-primary/5' : 'border-base-content/20 hover:border-primary/40'}"
        on:click={() => fileInput.click()}
        on:keydown={(e) => e.key === 'Enter' && fileInput.click()}
        role="button"
        tabindex="0"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="mx-auto h-12 w-12 text-base-content/30 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
        </svg>
        <p class="text-base-content/60 font-medium">Drop a file here or click to upload</p>
        <p class="text-base-content/40 text-sm mt-1">Supports .txt and .csv files</p>
        <p class="text-base-content/40 text-sm mt-3">— or paste your workout text below —</p>
      </div>
    {/if}

    <textarea
      class="textarea textarea-bordered w-full rounded-xl min-h-[300px] font-mono text-sm mt-3"
      placeholder="Paste your workout here...

Example:
Monday 3/18
Squat - Barbell
  5x5 @ 225 lbs
Bench Press - Dumbbell, Incline
  4x8 @ 60 lbs RPE 7
Running
  3 miles in 24:00"
      bind:value={$rawText}
    />
  </div>

  <input
    bind:this={fileInput}
    type="file"
    accept=".txt,.csv"
    class="hidden"
    on:change={handleFileInput}
  />

  <!-- Character count -->
  {#if $rawText}
    <p class="text-sm text-base-content/40 text-right">{$rawText.length} characters</p>
  {/if}

  <!-- Error message -->
  {#if $errorMsg}
    <div class="alert alert-error rounded-xl">
      <span>{$errorMsg}</span>
    </div>
  {/if}

  <!-- Parse button -->
  <div class="flex justify-end">
    <button
      class="btn btn-primary rounded-xl"
      disabled={$isLoading || !$rawText.trim()}
      on:click={parseInput}
    >
      {#if $isLoading}
        <span class="loading loading-spinner loading-sm"></span>
        Parsing...
      {:else}
        Parse Workout
      {/if}
    </button>
  </div>
</div>
