<script>
    import { goto } from "$app/navigation";
    import ActivityTracker from "$lib/ActivityTracker.svelte";
    import { fetchBootUserDto, bootUserDto } from "$lib/stores";
    import { onMount } from "svelte";
  import "tailwindcss/tailwind.css";

  onMount(async () => {
        await fetchBootUserDto(fetch)
        //console.log("BootUserDto:  ", $bootUserDto)
        if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
            goto('/auth');
        } 
    });
</script>

<!---------------------------------------------------------------------------->
<nav class="bg-primary-content" >
  <div class="mx-auto max-w-7xl px-2 sm:px-6 lg:px-8">
    <div class="relative flex h-16 items-center justify-between">
      
      <div class="flex flex-1 items-center justify-center sm:items-stretch sm:justify-start">
        <div class="flex flex-shrink-0 items-center text-primary">
          <img
            class="h-10 w-auto mr-2"
            src="/src/assets/logo.png"
            alt="Lion Logo"
          />
          <a href="/" class="title text-2xl tracking-widest text-bold"
            >LIONHEART</a
          >
        </div>
        <div class="hidden sm:ml-6 sm:block">
          <div class="flex space-x-4">
            <!-- Current: "bg-gray-900 text-white", Default: "text-gray-300 hover:bg-gray-700 hover:text-white" -->
            <a
              href="/"
              class="text-primary hover:bg-primary hover:text-white rounded-md px-3 py-2 text-sm font-medium "
              >Homebase</a
            >
            <a
              href="/activity"
              class="text-primary hover:bg-primary hover:text-white rounded-md px-3 py-2 text-sm font-medium"
              >Activities</a
            >
            <a
              href="/wellness"
              class="text-primary hover:bg-primary hover:text-white rounded-md px-3 py-2 text-sm font-medium"
              >Wellness </a
            >
            
            
            <!-- <a
              href="./page.svelte"
              class="text-primary hover:bg-primary hover:text-white rounded-md px-3 py-2 text-sm font-medium"
              >Logbook</a
            > -->
            
          </div>
        </div>
      </div>
      <div class="absolute inset-y-0 right-0 flex items-center pr-2 sm:static sm:inset-auto sm:ml-6 sm:pr-0 flex-none">
        
        <!-- Profile dropdown -->
        <div class="relative ml-3">
          <div>
            <button
              type="button"
              class="relative flex rounded-full bg-base-800 text-sm focus:outline-none focus:ring-2 focus:ring-white focus:ring-offset-2 focus:ring-offset-gray-800"
              id="user-menu-button"
              aria-expanded="false"
              aria-haspopup="true"
            >
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
  <div class="sm:hidden text-center" >
    <div class="space-y-1 px-2 pb-3 pt-2">
      <!-- Current: "bg-gray-900 text-white", Default: "text-gray-300 hover:bg-gray-700 hover:text-white" -->
      <a
        href="/"
        class="text-primary hover:bg-primary hover:text-white block rounded-md px-3 py-2 text-base font-medium"
        >Homebase</a
      >
      <a
        href="/activity"
        class="text-primary hover:bg-primary hover:text-white block rounded-md px-3 py-2 text-base font-medium"
        >Activites</a
      >
      <a
        href="/wellness"
        class="text-primary hover:bg-primary hover:text-white block rounded-md px-3 py-2 text-base font-medium"
        >Wellness Tracker</a
      >
      
    </div>
  </div>
</nav>


<slot />


