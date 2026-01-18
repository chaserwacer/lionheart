<script>
  import { goto } from "$app/navigation";
  import { bootUserDto } from "$lib/stores/stores";
  import { onMount } from "svelte";
  import { themeChange } from "theme-change";

  let applicationName = "";
  let personalApiAccessToken = "";
  let showAccessTokenAdded = false;
  let showApiAccessModal = false;
  let currentTheme = "";

  const themes = [
    { value: "bumblebee", label: "Bumblebee" },
    { value: "black", label: "Black" },
    { value: "caramellatte", label: "Caramellatte" }
  ];

  function showApiModal() {
    showApiAccessModal = true;
  }

  function closeApiModal() {
    showApiAccessModal = false;
  }

  async function logout() {
    try {
      const response = await self.fetch("/api/user/logout", {
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

  async function addPersonalApiAccessToken() {
    try {
      const url = "/api/user/set-personal-api-access-token";
      const body = {
        applicationName,
        accessToken: personalApiAccessToken
      };
      const response = await self.fetch(url, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(body)
      });
      if (response.ok) {
        applicationName = "";
        personalApiAccessToken = "";
        showAccessTokenAdded = true;
        closeApiModal();
      } else {
        console.error("Failed to create token:", response.statusText);
      }
    } catch (error) {
      console.error("Error creating access:", error);
    }
  }

  onMount(() => {
    // Get the current theme from the document
    // currentTheme = document.documentElement.getAttribute("data-theme") || "bumblebee";
    themeChange(false);
    showAccessTokenAdded = false;
  });
</script>

<svelte:head>
  <title>Profile</title>
</svelte:head>

<div class="mx-auto ">
  <div class="max-w-2xl mx-auto space-y-6">
    <!-- Header Card -->
    <div class="card bg-base-100 shadow-editorial-lg border border-base-300">
      <div class="card-body items-center text-center">
        <h1 class="text-3xl sm:text-4xl font-display font-black tracking-tight mb-2">Welcome, {$bootUserDto.name}</h1>
        <p class="text-base-content/60 text-sm uppercase tracking-wider font-bold">Manage your profile settings</p>
      </div>
    </div>

    <!-- Success Alert -->
    {#if showAccessTokenAdded}
      <div role="alert" class="alert alert-success">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6 shrink-0 stroke-current" fill="none" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        <span>Personal Access Token added successfully!</span>
        <button class="btn btn-sm btn-ghost" on:click={() => showAccessTokenAdded = false}>✕</button>
      </div>
    {/if}

    

    <!-- API Integration Card -->
    <div class="card bg-base-100 shadow-editorial-lg border border-base-300">
      <div class="card-body">
        <h2 class="text-xl font-bold uppercase tracking-wider mb-1">
          Integrations
        </h2>
        <p class="text-xs text-base-content/60 mb-6 uppercase tracking-wider font-bold">Connect external services</p>

        <button class="btn btn-primary w-full sm:w-auto text-xs font-bold uppercase tracking-wider" on:click={showApiModal}>
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          Add API Access Token
        </button>
      </div>
    </div>

    <!-- Account Actions Card -->
    <div class="card bg-base-100 shadow-editorial-lg border border-base-300">
      <div class="card-body">
        <h2 class="text-xl font-bold uppercase tracking-wider mb-1">
          Account
        </h2>
        <p class="text-xs text-base-content/60 mb-6 uppercase tracking-wider font-bold">Manage your account settings</p>

        <button class="btn btn-outline btn-error w-full sm:w-auto text-xs font-bold uppercase tracking-wider" on:click={logout}>
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
          </svg>
          Logout
        </button>
      </div>
    </div>
  </div>
</div>

<!-- API Access Token Modal -->
{#if showApiAccessModal}
  <div class="modal modal-open">
    <div class="modal-box w-11/12 max-w-md">
      <button class="btn btn-sm btn-circle btn-ghost absolute right-2 top-2" on:click={closeApiModal}>✕</button>

      <h3 class="text-xl font-bold uppercase tracking-wider mb-2">Add API Access Token</h3>
      <p class="text-xs text-base-content/60 mb-6 uppercase tracking-wider font-bold">
        Connect external services by adding your personal access tokens.
        See the API provider's documentation for token generation.
      </p>

      <form on:submit|preventDefault={addPersonalApiAccessToken}>
        <div class="form-control w-full mb-4">
          <label class="label" for="app-name">
            <span class="label-text text-xs font-bold uppercase tracking-wider">Application</span>
          </label>
          <select
            id="app-name"
            class="select select-bordered w-full"
            bind:value={applicationName}
            required
          >
            <option value="" disabled>Select an application</option>
            <option value="oura">Oura</option>
          </select>
        </div>

        <div class="form-control w-full mb-6">
          <label class="label" for="access-token">
            <span class="label-text text-xs font-bold uppercase tracking-wider">Access Token</span>
          </label>
          <input
            id="access-token"
            type="text"
            placeholder="Enter your access token"
            class="input input-bordered w-full"
            required
            bind:value={personalApiAccessToken}
          />
        </div>

        <div class="modal-action space-x-4">
          <button type="button" class="btn btn-outline text-xs font-bold uppercase tracking-wider" on:click={closeApiModal}>Cancel</button>
          <button type="submit" class="btn btn-primary text-xs font-bold uppercase tracking-wider">Save Token</button>
        </div>
      </form>
    </div>
    <div
      class="modal-backdrop bg-base-300/80"
      role="button"
      tabindex="0"
      aria-label="Close modal"
      on:click={closeApiModal}
      on:keydown={(e) => (e.key === 'Escape' || e.key === 'Enter' || e.key === ' ') && closeApiModal()}
    ></div>
  </div>
{/if}
