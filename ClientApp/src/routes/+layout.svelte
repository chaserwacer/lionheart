<script>
  import { goto } from "$app/navigation";
  import ActivityTracker from "$lib/ActivityTracker.svelte";
    import OuraSync from "$lib/OuraSync.svelte";
  import { fetchBootUserDto, bootUserDto } from "$lib/stores";
  import WellnessTracker from "$lib/WellnessTracker.svelte";
  import { onMount } from "svelte";


  import "tailwindcss/tailwind.css";

  onMount(async () => {
    await fetchBootUserDto(fetch);
    //console.log("BootUserDto:  ", $bootUserDto)
    if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
      goto("/auth");
    }
  });
</script>

<!---------------------------------------------------------------------------->
<nav class="bg-primary-content border-b border-primary">
  <div class="mx-auto max-w-7xl px-2 sm:px-6 lg:px-8">
    <div class="relative flex h-16 items-center">
      <div
        class="flex flex-1 items-center justify-center md:items-stretch md:justify-start"
      >
        <div class="flex flex-shrink-0 items-center text-primary">
          <img
            class="h-10 w-auto mr-2"
            src="/src/assets/logo.png"
            alt="Lion Logo"
          />
          <a href="/" class="title text-2xl tracking-widest text-bold"
            >LIONHEART</a
          >

          <div class="pt-2.5 ml-2 md:hidden">
            <div>
              <button>
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
        <div class="hidden md:ml-10 md:block pt-1.5">
          <div class="flex space-x-4">
            <!-- <a
              href="/"
              class="text-primary hover:bg-primary hover:text-white rounded-md px-3 py-2 text-sm font-medium "
              >Homebase</a
            > -->
            <ActivityTracker />
            <WellnessTracker />
            <OuraSync />
            
          </div>
        </div>
      </div>
      <div class="flex justify-end">
        <!-- Profile btn -->
        <div class="pt-2.5 hidden md:block">
          <div>
            <button>
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
  <div class="md:hidden text-center">
    <div class="mb-5">
      <ActivityTracker />
      <WellnessTracker />
      <OuraSync />
    </div>
  </div>
</nav>

<slot />
