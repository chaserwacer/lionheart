<script>
    import { goto } from '$app/navigation';
    import { onMount } from 'svelte';
    import { themeChange } from 'theme-change';

    async function logout() {
        try {
            const response = await self.fetch("/api/user/logoutuser", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
            });

            if (response.ok) {
              goto("/");
            } else {
                console.error("Failed to logout:", response.statusText);
            }
        } catch (error) {
            console.error("Error logging out:", error);
        }
    }

    onMount(() => {
      themeChange(false); // False parameter is required for Svelte
    });
  </script>

<svelte:head>
  <title>Profile</title>
</svelte:head>
  
  
  <div class="flex flex-col items-center text-center space-y-5 pt-10 ">
    <div class=""><h1 class="text-4xl text-primary font-thin tracking-widest italic">Style Shifter</h1></div>
    <div class="space-x-2 space-y-2">
      <button class="btn btn-secondary" data-set-theme="default" data-act-class="btn-active">Default</button>
      <button class="btn btn-secondary" data-set-theme="autumn" data-act-class="btn-active">Autumn</button>
      <button class="btn btn-secondary" data-set-theme="lofi" data-act-class="btn-active">White</button>
      <button class="btn btn-secondary" data-set-theme="nord" data-act-class="btn-active">Nord</button>
      <button class="btn btn-secondary" data-set-theme="black" data-act-class="btn-active">Black</button>
      <button class="btn btn-secondary" data-set-theme="retro" data-act-class="btn-active">Retro</button>
      <button class="btn btn-secondary" data-set-theme="light" data-act-class="btn-active">Light</button>
      <button class="btn btn-secondary" data-set-theme="dark" data-act-class="btn-active">Dark</button>
      <button class="btn btn-secondary" data-set-theme="valentine" data-act-class="btn-active">Valentine</button>
      <button class="btn btn-secondary" data-set-theme="aqua" data-act-class="btn-active">Aqua</button>
      <button class="btn btn-secondary" data-set-theme="forest" data-act-class="btn-active">Forest</button>
      <button class="btn btn-secondary" data-set-theme="synthwave" data-act-class="btn-active">Synthwave</button>
      
    </div>
    <div class="divider divider-neutral"></div>
    <div class=""><h1 class="text-4xl text-primary font-bold">Info Management</h1></div>
    <button class="btn btn-primary" on:click={logout}>Logout</button>
  
    <!-- <select class="select select-primary" data-choose-theme>
      <option value="default">Default</option>
      <option value="autumn">Autumn</option>
      <option value="retro">Retro</option>
      <option value="light">Light</option>
      <option value="dark">Dark</option>
      <option value="valentine">Valentine</option>
      <option value="aqua">Aqua</option>
      <option value="forest">Forest</option>
    </select> -->
  </div>
