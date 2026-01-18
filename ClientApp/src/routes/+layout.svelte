<script>
  import { goto } from "$app/navigation";
  import { page } from "$app/stores";
  import OuraSync from "$lib/components/OuraSync.svelte";
  import { fetchBootUserDto, bootUserDto } from "$lib/stores/stores";
  import { onMount } from "svelte";
  import "tailwindcss/tailwind.css";

  $: isAuthPage = $page.url.pathname === "/auth";

  onMount(async () => {
    await fetchBootUserDto(fetch);
    if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
      goto("/auth");
    }
  });


</script>

<!---------------------------------------------------------------------------->
<div class="flex flex-col min-h-screen h-screen">
{#if !isAuthPage}
<nav class="bg-primary-content border-b border-primary">
  <div class="mx-auto max-w-7xl px-2 sm:px-6 lg:px-8">
    <div class="relative flex h-16 items-center">
      <div
        class="flex flex-1 items-center justify-center lg:items-stretch lg:justify-start"
      >
        <div class="flex flex-shrink-0 items-center text-primary">
          <img
            class="h-10 w-auto mr-2"
            src="/src/assets/logo.png"
            alt="Lion Logo"
          />
          <a href="/training" class="title text-2xl tracking-widest text-bold"
            >LIONHEART</a
          >

          <div class="pt-2.5 ml-2 lg:hidden" >
            <div>
              <button class="" on:click={() => goto('/profile')}>
                <a href="/profile"
                  ><img
                    class="h-10 w-auto rounded-full"
                    src="/src/assets/lion-profile.png"
                    alt="Profile"
                  /></a
                > 
              </button>
            </div>
          </div>
        </div>
        <div class="hidden lg:ml-10 lg:block pt-1.5">
          <div class="flex space-x-4">
            <!-- <a
              href="/"
              class="text-primary hover:bg-primary hover:text-white rounded-md px-3 py-2 text-sm font-medium "
              >Homebase</a
            > -->
            <button class="btn btn-sm btn-primary" on:click={() => goto('/training')}>Training</button>
            <!-- <ActivityTracker />
            <WellnessTracker selectedDate={$wellnessStateDate}/>
            <OuraSync />
            <button class="btn btn-sm btn-outline btn-primary" on:click={() => goto('/programs')}>Programs</button>
            <button class="btn btn-sm btn-outline btn-primary" on:click={() => goto('/injuryPortal')}>Injury</button>
            <button class="btn btn-sm btn-outline btn-primary" on:click={() => goto('/chat')}>HAL</button> -->
          </div>
        </div>
      </div>
      <div class="flex justify-end">
        <!-- Profile btn -->
        <div class="pt-2.5 hidden lg:block">
          <div>
            <button on:click={() => goto('/profile')}>
              <a href="/profile"
                ><img
                  class="h-10 w-auto rounded-full"
                  src="/src/assets/lion-profile.png"
                  alt="Profile"
                /></a
              >
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>

  <!-- Mobile menu, show/hide based on menu state. -->
  <div class="lg:hidden text-center">
    <div class="mb-5 gap-2 flex justify-center flex-wrap">
      <button class="btn btn-sm btn-primary" on:click={() => goto('/training')}>Training</button>
      <!-- <ActivityTracker />
      <WellnessTracker selectedDate={$wellnessStateDate}/>
      <OuraSync />
      <button class="btn btn-sm btn-outline btn-primary" on:click={() => goto('/programs')}>Programs</button>
      <button class="btn btn-sm btn-outline btn-primary" on:click={() => goto('/injuryPortal')}>Injury</button> -->
    </div>
  </div>
</nav>
{/if}
<main class="flex flex-1 overflow-y-auto w-full">
    <slot />
  </main>
</div>

