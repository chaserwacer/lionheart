<script>
  import { goto } from "$app/navigation";
  import { page } from "$app/stores";
  import { fetchBootUserDto, bootUserDto } from "$lib/stores/stores";
  import { theme, themePreference, toggleTheme } from "$lib/stores/themeStore";
  import { onMount } from "svelte";
  import "tailwindcss/tailwind.css";

  $: isAuthPage = $page.url.pathname === "/auth";
  $: currentPath = $page.url.pathname;

  let drawerOpen = false;

  function closeDrawer() {
    drawerOpen = false;
  }

  /**
     * @param {string | URL} path
     */
  function navigateTo(path) {
    closeDrawer();
    goto(path);
  }

  onMount(async () => {
    await fetchBootUserDto(fetch);
    if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
      goto("/auth");
    }
  });

  // Get theme icon based on preference
  $: themeIcon = $themePreference === 'system' ? 'auto' : $themePreference === 'dark' ? 'dark' : 'light';
</script>

<div class="flex flex-col min-h-screen h-screen bg-base-200">
  {#if !isAuthPage}
    <!-- Mobile Drawer -->
    <div class="drawer lg:drawer-open">
      <input id="nav-drawer" type="checkbox" class="drawer-toggle" bind:checked={drawerOpen} />

      <!-- Main Content Area -->
      <div class="drawer-content flex flex-col">
        <!-- Top Navbar -->
        <nav class="sticky top-0 z-30 backdrop-blur-md bg-base-100/80 border-b border-base-300">
          <div class="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
            <div class="flex h-16 items-center justify-between">
              <!-- Mobile menu button -->
              <label for="nav-drawer" class="btn btn-ghost btn-square lg:hidden">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" class="w-6 h-6 stroke-current">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16"></path>
                </svg>
              </label>

              <!-- Logo -->
              <a href="/" class="flex items-center  group">
                
                <span class="text-2xl font-display font-black tracking-tightest text-base-content  sm:block tracking-widest">
                  LIONHEART
                </span>
              </a>

              <!-- Desktop Navigation -->
              <div class="hidden lg:flex items-center gap-1">
                <a
                  href="/"
                  class="px-5 py-2.5 text-sm font-bold uppercase tracking-wider transition-all duration-200
                         {currentPath === '/' ? 'text-base-content border-b-2 border-base-content' : 'text-base-content/60 hover:text-base-content border-b-2 border-transparent'}"
                >
                  Home
                </a>
                <a
                  href="/training"
                  class="px-5 py-2.5 text-sm font-bold uppercase tracking-wider transition-all duration-200
                         {currentPath === '/training' || currentPath.startsWith('/training') ? 'text-base-content border-b-2 border-base-content' : 'text-base-content/60 hover:text-base-content border-b-2 border-transparent'}"
                >
                  Training
                </a>
                <a
                  href="/activities"
                  class="px-5 py-2.5 text-sm font-bold uppercase tracking-wider transition-all duration-200
                         {currentPath === '/activities' ? 'text-base-content border-b-2 border-base-content' : 'text-base-content/60 hover:text-base-content border-b-2 border-transparent'}"
                >
                  Activities
                </a>
                <a
                  href="/injuries"
                  class="px-5 py-2.5 text-sm font-bold uppercase tracking-wider transition-all duration-200
                         {currentPath === '/injuries' ? 'text-base-content border-b-2 border-base-content' : 'text-base-content/60 hover:text-base-content border-b-2 border-transparent'}"
                >
                  Injuries
                </a>
                <a
                  href="/wellness"
                  class="px-5 py-2.5 text-sm font-bold uppercase tracking-wider transition-all duration-200
                         {currentPath === '/wellness' ? 'text-base-content border-b-2 border-base-content' : 'text-base-content/60 hover:text-base-content border-b-2 border-transparent'}"
                >
                  Wellness
                </a>
                <a
                  href="/oura"
                  class="px-5 py-2.5 text-sm font-bold uppercase tracking-wider transition-all duration-200
                         {currentPath === '/oura' ? 'text-base-content border-b-2 border-base-content' : 'text-base-content/60 hover:text-base-content border-b-2 border-transparent'}"
                >
                  Oura
                </a>
                <a
                  href="/profile"
                  class="px-5 py-2.5 text-sm font-bold uppercase tracking-wider transition-all duration-200
                         {currentPath === '/profile' ? 'text-base-content border-b-2 border-base-content' : 'text-base-content/60 hover:text-base-content border-b-2 border-transparent'}"
                >
                  Profile
                </a>
              </div>

              <!-- Right side: Theme toggle + Profile -->
              <div class="flex items-center gap-2">
                <!-- Theme Toggle -->
                <button
                  on:click={toggleTheme}
                  class="btn btn-ghost btn-square btn-sm rounded-xl"
                  title="Toggle theme ({themeIcon})"
                >
                  {#if $themePreference === 'system'}
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M9 17.25v1.007a3 3 0 0 1-.879 2.122L7.5 21h9l-.621-.621A3 3 0 0 1 15 18.257V17.25m6-12V15a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 15V5.25m18 0A2.25 2.25 0 0 0 18.75 3H5.25A2.25 2.25 0 0 0 3 5.25m18 0V12a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 12V5.25" />
                    </svg>
                  {:else if $themePreference === 'dark'}
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M21.752 15.002A9.72 9.72 0 0 1 18 15.75c-5.385 0-9.75-4.365-9.75-9.75 0-1.33.266-2.597.748-3.752A9.753 9.753 0 0 0 3 11.25C3 16.635 7.365 21 12.75 21a9.753 9.753 0 0 0 9.002-5.998Z" />
                    </svg>
                  {:else}
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M12 3v2.25m6.364.386-1.591 1.591M21 12h-2.25m-.386 6.364-1.591-1.591M12 18.75V21m-4.773-4.227-1.591 1.591M5.25 12H3m4.227-4.773L5.636 5.636M15.75 12a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0Z" />
                    </svg>
                  {/if}
                </button>

                
              </div>
            </div>
          </div>
        </nav>

        <!-- Page Content -->
        <main class="flex-1 overflow-y-auto">
          <slot />
        </main>
      </div>

      <!-- Drawer Sidebar (Mobile) -->
      <div class="drawer-side z-40 lg:hidden">
        <label for="nav-drawer" aria-label="close sidebar" class="drawer-overlay"></label>
        <aside class="bg-base-100 min-h-full w-72 p-6">
          <!-- Drawer Header -->
          <div class="flex items-center gap-3 mb-12">
            <span class="text-2xl font-display font-black tracking-widest">LIONHEART</span>
          </div>

          <!-- Navigation Links -->
          <nav class="flex flex-col gap-1">
            <button
              on:click={() => navigateTo('/')}
              class="px-4 py-3 text-sm font-bold uppercase tracking-wider text-left transition-all duration-200 rounded-lg
                     {currentPath === '/' ? 'text-base-content bg-base-200/60' : 'text-base-content/60 hover:text-base-content hover:bg-base-200/40'}"
            >
              Home
            </button>

            <button
              on:click={() => navigateTo('/training')}
              class="px-4 py-3 text-sm font-bold uppercase tracking-wider text-left transition-all duration-200 rounded-lg
                     {currentPath === '/training' || currentPath.startsWith('/training') ? 'text-base-content bg-base-200/60' : 'text-base-content/60 hover:text-base-content hover:bg-base-200/40'}"
            >
              Training
            </button>

            <button
              on:click={() => navigateTo('/activities')}
              class="px-4 py-3 text-sm font-bold uppercase tracking-wider text-left transition-all duration-200 rounded-lg
                     {currentPath === '/activities' ? 'text-base-content bg-base-200/60' : 'text-base-content/60 hover:text-base-content hover:bg-base-200/40'}"
            >
              Activities
            </button>

            <button
              on:click={() => navigateTo('/injuries')}
              class="px-4 py-3 text-sm font-bold uppercase tracking-wider text-left transition-all duration-200 rounded-lg
                     {currentPath === '/injuries' ? 'text-base-content bg-base-200/60' : 'text-base-content/60 hover:text-base-content hover:bg-base-200/40'}"
            >
              Injuries
            </button>

            <button
              on:click={() => navigateTo('/wellness')}
              class="px-4 py-3 text-sm font-bold uppercase tracking-wider text-left transition-all duration-200 rounded-lg
                     {currentPath === '/wellness' ? 'text-base-content bg-base-200/60' : 'text-base-content/60 hover:text-base-content hover:bg-base-200/40'}"
            >
              Wellness
            </button>

            <button
              on:click={() => navigateTo('/oura')}
              class="px-4 py-3 text-sm font-bold uppercase tracking-wider text-left transition-all duration-200 rounded-lg
                     {currentPath === '/oura' ? 'text-base-content bg-base-200/60' : 'text-base-content/60 hover:text-base-content hover:bg-base-200/40'}"
            >
              Oura
            </button>

            <button
              on:click={() => navigateTo('/profile')}
              class="px-4 py-3 text-sm font-bold uppercase tracking-wider text-left transition-all duration-200 rounded-lg
                     {currentPath === '/profile' ? 'text-base-content bg-base-200/60' : 'text-base-content/60 hover:text-base-content hover:bg-base-200/40'}"
            >
              Profile
            </button>
          </nav>

          <!-- Theme Toggle in Drawer -->
          <div class="mt-auto pt-8 border-t border-base-300">
            <p class="text-xs font-bold uppercase tracking-wider text-base-content/50 mb-4 px-4">Appearance</p>
            <div class="flex gap-2 px-4">
              <button
                on:click={() => themePreference.set('light')}
                class="flex-1 py-2.5 rounded-lg text-xs font-bold uppercase tracking-wider transition-all duration-200
                       {$themePreference === 'light' ? 'bg-base-content text-base-100' : 'bg-base-200 hover:bg-base-300 text-base-content/70'}"
              >
                Light
              </button>
              <button
                on:click={() => themePreference.set('dark')}
                class="flex-1 py-2.5 rounded-lg text-xs font-bold uppercase tracking-wider transition-all duration-200
                       {$themePreference === 'dark' ? 'bg-base-content text-base-100' : 'bg-base-200 hover:bg-base-300 text-base-content/70'}"
              >
                Dark
              </button>
              <button
                on:click={() => themePreference.set('system')}
                class="flex-1 py-2.5 rounded-lg text-xs font-bold uppercase tracking-wider transition-all duration-200
                       {$themePreference === 'system' ? 'bg-base-content text-base-100' : 'bg-base-200 hover:bg-base-300 text-base-content/70'}"
              >
                Auto
              </button>
            </div>
          </div>

          
        </aside>
      </div>
    </div>
  {:else}
    <!-- Auth page - no navigation -->
    <main class="flex-1 overflow-y-auto">
      <slot />
    </main>
  {/if}
</div>
