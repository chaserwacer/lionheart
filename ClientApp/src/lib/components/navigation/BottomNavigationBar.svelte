<script lang="ts">
  import NavigationIcon from './NavigationIcon.svelte';
  import {
    isDestinationActive,
    primaryDestinations,
    secondaryDestinations
  } from './navigationDestinations';

  let { currentPath }: { currentPath: string } = $props();

  const moreIconPath =
    'M6.75 12a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0ZM12.75 12a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0ZM18.75 12a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0Z';

  let moreSheetOpen = $state(false);

  const inSecondarySection = $derived(
    secondaryDestinations.some((destination) => isDestinationActive(currentPath, destination))
  );
</script>

{#if moreSheetOpen}
  <div class="fixed inset-0 z-40 lg:hidden">
    <button
      class="absolute inset-0 h-full w-full bg-black/40"
      aria-label="Close menu"
      onclick={() => (moreSheetOpen = false)}
    ></button>

    <nav
      class="absolute inset-x-0 bottom-0 rounded-t-2xl border-t border-base-300 bg-base-100 p-2 pb-[calc(0.5rem+env(safe-area-inset-bottom))] shadow-editorial-lg"
      aria-label="More destinations"
    >
      <div class="mx-auto mb-2 h-1 w-10 rounded-full bg-base-content/20"></div>
      {#each secondaryDestinations as destination (destination.path)}
        <a
          href={destination.path}
          onclick={() => (moreSheetOpen = false)}
          aria-current={isDestinationActive(currentPath, destination) ? 'page' : undefined}
          class="flex min-h-[3rem] items-center gap-3 rounded-xl px-4 text-sm font-bold uppercase tracking-wider transition-colors
                 {isDestinationActive(currentPath, destination)
                   ? 'bg-base-200 text-base-content'
                   : 'text-base-content/70 active:bg-base-200'}"
        >
          <NavigationIcon path={destination.iconPath} class="h-5 w-5" />
          {destination.label}
        </a>
      {/each}
    </nav>
  </div>
{/if}

<nav
  class="shrink-0 border-t border-base-300 bg-base-100 pb-[env(safe-area-inset-bottom)] lg:hidden"
  aria-label="Primary"
>
  <div class="flex items-stretch">
    {#each primaryDestinations as destination (destination.path)}
      <a
        href={destination.path}
        aria-current={isDestinationActive(currentPath, destination) ? 'page' : undefined}
        class="flex min-h-[3.5rem] flex-1 flex-col items-center justify-center gap-1 px-1 py-1.5 text-[0.625rem] font-bold uppercase tracking-wider transition-colors
               {isDestinationActive(currentPath, destination)
                 ? 'text-base-content'
                 : 'text-base-content/50'}"
      >
        <NavigationIcon path={destination.iconPath} class="h-6 w-6" />
        {destination.label}
      </a>
    {/each}

    <button
      onclick={() => (moreSheetOpen = !moreSheetOpen)}
      aria-expanded={moreSheetOpen}
      class="flex min-h-[3.5rem] flex-1 flex-col items-center justify-center gap-1 px-1 py-1.5 text-[0.625rem] font-bold uppercase tracking-wider transition-colors
             {moreSheetOpen || inSecondarySection ? 'text-base-content' : 'text-base-content/50'}"
    >
      <NavigationIcon path={moreIconPath} class="h-6 w-6" />
      More
    </button>
  </div>
</nav>
