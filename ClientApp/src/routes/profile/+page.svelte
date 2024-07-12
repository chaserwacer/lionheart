<script>
    import { onMount } from 'svelte';
    import { themeChange } from 'theme-change';
  
    onMount(() => {
      themeChange(false); // False parameter is required for Svelte
    });
  </script>

<svelte:head>
  <title>Profile</title>
</svelte:head>
  
  
  <div class="flex flex-col items-center text-center space-y-5 space-x-5 p-10 ">
    <div class=""><h1 class="text-4xl text-primary font-thin tracking-widest italic">Style Shifter</h1></div>
    <div class="space-x-2 space-y-2">
      <button class="btn btn-secondary" data-set-theme="default" data-act-class="btn-active">Default</button>
      <button class="btn btn-secondary" data-set-theme="autumn" data-act-class="btn-active">Autumn</button>
      <button class="btn btn-secondary" data-set-theme="retro" data-act-class="btn-active">Retro</button>
      <button class="btn btn-secondary" data-set-theme="light" data-act-class="btn-active">Light</button>
      <button class="btn btn-secondary" data-set-theme="dark" data-act-class="btn-active">Dark</button>
      <button class="btn btn-secondary" data-set-theme="valentine" data-act-class="btn-active">Valentine</button>
      <button class="btn btn-secondary" data-set-theme="aqua" data-act-class="btn-active">Aqua</button>
      <button class="btn btn-secondary" data-set-theme="forest" data-act-class="btn-active">Forest</button>
      <button class="btn btn-secondary" data-set-theme="synthwave" data-act-class="btn-active">Synthwave</button>
    </div>
  
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
  <div class="divider divider-neutral"></div>