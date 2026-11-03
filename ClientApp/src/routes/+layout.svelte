<script lang="ts">
  import { goto } from "$app/navigation";
  import { page } from "$app/state";
  import BottomNavigationBar from "$lib/components/navigation/BottomNavigationBar.svelte";
  import TopNavigationBar from "$lib/components/navigation/TopNavigationBar.svelte";
  import { fetchBootUserDto, bootUserDto } from "$lib/stores/stores";
  import { theme } from "$lib/stores/themeStore";
  import { onMount } from "svelte";
  import type { Snippet } from "svelte";
  import "../app.css";

  // daisyUI's base-100 for each theme the app ships; iOS paints the standalone
  // status bar with theme-color, so it has to track whichever one is active.
  const statusBarColors: Record<string, string> = {
    bumblebee: "#ffffff",
    dark: "#1d232a",
  };

  let { children }: { children?: Snippet } = $props();

  const currentPath = $derived(page.url.pathname);
  const isAuthPage = $derived(currentPath === "/auth");

  $effect(() => {
    document
      .querySelector('meta[name="theme-color"]')
      ?.setAttribute("content", statusBarColors[$theme] ?? statusBarColors.bumblebee);
  });

  onMount(async () => {
    await fetchBootUserDto(fetch);
    if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
      goto("/auth");
      return;
    }

    // On a phone the app opens on Quick Log, not on the dashboard (docs/quick-log.md). The
    // dashboard stays reachable, and stays the landing screen where there is room for it.
    if (currentPath === "/" && window.matchMedia("(max-width: 767px)").matches) {
      goto("/log", { replaceState: true });
    }
  });
</script>

<!-- dvh rather than vh so the shell tracks iOS Safari's collapsing toolbars. -->
<div
  class="flex h-dvh flex-col overflow-x-hidden bg-base-200 pl-[env(safe-area-inset-left)] pr-[env(safe-area-inset-right)]"
>
  {#if isAuthPage}
    <main class="flex-1 overflow-y-auto overflow-x-hidden">
      {@render children?.()}
    </main>
  {:else}
    <TopNavigationBar {currentPath} />

    <main class="flex-1 overflow-y-auto overflow-x-hidden">
      {@render children?.()}
    </main>

    <BottomNavigationBar {currentPath} />
  {/if}
</div>
