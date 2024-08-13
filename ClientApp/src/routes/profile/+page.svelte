<script>
  import { goto } from "$app/navigation";
  import { bootUserDto } from "$lib/stores";
  import { onMount } from "svelte";
  import { themeChange } from "theme-change";

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
<div class="flex justify-center md:text-left p-5 text-center ">
  <article class="prose lg:prose-xl ">
    <h1>Lionheart Profile</h1>
    <h4>Welcome, {$bootUserDto.name}, to Project Lionheart.</h4>
    <p>
      Lionheart is an application for storing and analyzing all of a users training data. Lionheart is to be developed to be able to store anything
      relevant to training, in the attempt to create an ecosystem that all of your data can live. This will then allow an athlete to analyze trends
      and data while remaining in one place, as opposed to having to navigate from app to app.

      <br><br>
      The home page will be your one-stop-shop viewing location for all of your
      training data. Select a date to view all associated data. 
    </p>

    <div class="divider"></div>
    <h2 class="">Info Management</h2>
    <p>
      Lionheart User Info:
    </p>


    <div class="flex flex-col justify-center items-center md:items-start">
      <button class="btn btn-primary w-40" on:click={logout}>Logout</button>
    <label class="form-control w-40">
      <div class="label">
        <span class="label-text font-bold">Theme Selector</span>
      </div>
    <select class="select select-primary" data-choose-theme>
        <option value="default">Default</option>
        <option value="lofi">White</option>
        <option value="black">Black</option>
        <option value="autumn">Autumn</option>
        <option value="nord">Blue</option> 
        <option value="retro">Retro</option>
        <!-- <option value=""></option>
        <option value=""></option> -->
      </select>
      </label>
    </div>
    

  
  </article>
</div>

<!-- <div class="flex flex-col items-center text-center space-y-5 pt-10">
  <div class="">
    
  </div>
  <div class="space-x-2 space-y-2">
    <button
      class="btn btn-secondary"
      data-set-theme="default"
      data-act-class="btn-active">Default</button
    >
    <button
      class="btn btn-secondary"
      data-set-theme="autumn"
      data-act-class="btn-active">Autumn</button
    >
    <button
      class="btn btn-secondary"
      data-set-theme="lofi"
      data-act-class="btn-active">White</button
    >
    <button
      class="btn btn-secondary"
      data-set-theme="nord"
      data-act-class="btn-active">Blue</button
    >
    <button
      class="btn btn-secondary"
      data-set-theme="black"
      data-act-class="btn-active">Black</button
    >
    <button
      class="btn btn-secondary"
      data-set-theme="retro"
      data-act-class="btn-active">Retro</button
    >
     <button class="btn btn-secondary" data-set-theme="light" data-act-class="btn-active">Light</button> -->
    <!-- <button class="btn btn-secondary" data-set-theme="dark" data-act-class="btn-active">Dark</button> -->
    <!-- <button -->
      <!-- class="btn btn-secondary"
      data-set-theme="valentine"
      data-act-class="btn-active">Valentine</button
    > -->
    <!-- <button class="btn btn-secondary" data-set-theme="aqua" data-act-class="btn-active">Aqua</button> -->
    <!-- <button class="btn btn-secondary" data-set-theme="forest" data-act-class="btn-active">Forest</button> -->
    <!-- <button class="btn btn-secondary" data-set-theme="synthwave" data-act-class="btn-active">Synthwave</button> -->
  <!-- </div> --> 
  
<!-- </div> -->
