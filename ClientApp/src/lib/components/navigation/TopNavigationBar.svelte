<script lang="ts">
  import { themePreference, toggleTheme } from '$lib/stores/themeStore';
  import { allDestinations, isDestinationActive } from './navigationDestinations';

  let { currentPath }: { currentPath: string } = $props();

  const themeLabel = $derived(
    $themePreference === 'system' ? 'system' : $themePreference === 'dark' ? 'dark' : 'light'
  );
</script>

<header
  class="sticky top-0 z-30 shrink-0 border-b border-base-300 bg-base-100/90 pt-[env(safe-area-inset-top)] backdrop-blur-md"
>
  <div class="mx-auto flex h-14 max-w-7xl items-center gap-2 px-4 sm:h-16 lg:px-8">
    <a href="/" class="flex min-h-11 items-center pr-2">
      <span class="font-display text-xl font-black tracking-widest text-base-content sm:text-2xl">
        LIONHEART
      </span>
    </a>

    <nav class="ml-auto hidden items-center gap-1 lg:flex" aria-label="Primary">
      {#each allDestinations as destination (destination.path)}
        <a
          href={destination.path}
          aria-current={isDestinationActive(currentPath, destination) ? 'page' : undefined}
          class="border-b-2 px-4 py-2.5 text-sm font-bold uppercase tracking-wider transition-colors duration-200
                 {isDestinationActive(currentPath, destination)
                   ? 'border-base-content text-base-content'
                   : 'border-transparent text-base-content/60 hover:text-base-content'}"
        >
          {destination.label}
        </a>
      {/each}
    </nav>

    <button
      onclick={toggleTheme}
      class="btn btn-ghost btn-square ml-auto h-11 min-h-11 w-11 rounded-xl lg:ml-2"
      title="Theme: {themeLabel}"
      aria-label="Switch theme (currently {themeLabel})"
    >
      {#if $themePreference === 'system'}
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="h-5 w-5" aria-hidden="true">
          <path stroke-linecap="round" stroke-linejoin="round" d="M9 17.25v1.007a3 3 0 0 1-.879 2.122L7.5 21h9l-.621-.621A3 3 0 0 1 15 18.257V17.25m6-12V15a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 15V5.25m18 0A2.25 2.25 0 0 0 18.75 3H5.25A2.25 2.25 0 0 0 3 5.25m18 0V12a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 12V5.25" />
        </svg>
      {:else if $themePreference === 'dark'}
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="h-5 w-5" aria-hidden="true">
          <path stroke-linecap="round" stroke-linejoin="round" d="M21.752 15.002A9.72 9.72 0 0 1 18 15.75c-5.385 0-9.75-4.365-9.75-9.75 0-1.33.266-2.597.748-3.752A9.753 9.753 0 0 0 3 11.25C3 16.635 7.365 21 12.75 21a9.753 9.753 0 0 0 9.002-5.998Z" />
        </svg>
      {:else}
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="h-5 w-5" aria-hidden="true">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 3v2.25m6.364.386-1.591 1.591M21 12h-2.25m-.386 6.364-1.591-1.591M12 18.75V21m-4.773-4.227-1.591 1.591M5.25 12H3m4.227-4.773L5.636 5.636M15.75 12a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0Z" />
        </svg>
      {/if}
    </button>
  </div>
</header>
