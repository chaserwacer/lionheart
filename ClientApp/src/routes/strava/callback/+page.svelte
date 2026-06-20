<script lang="ts">
    import { onMount } from "svelte";
    import { goto } from "$app/navigation";
    import { page } from "$app/stores";

    let error = "";
    let status = "Connecting your Strava account...";

    onMount(async () => {
        const params = $page.url.searchParams;
        const code = params.get("code");
        const state = params.get("state");
        const denied = params.get("error");

        if (denied) {
            error = "Strava authorization was denied.";
            return;
        }
        if (!code || !state) {
            error = "Missing authorization details from Strava.";
            return;
        }

        try {
            const response = await fetch("/api/strava/connect", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ code, state }),
            });

            if (response.ok) {
                status = "Connected! Redirecting...";
                goto("/strava");
            } else {
                error =
                    "Failed to connect your Strava account. Please try again.";
            }
        } catch (err) {
            console.error(err);
            error = "An error occurred while connecting to Strava.";
        }
    });
</script>

<svelte:head>
    <title>Connecting Strava</title>
</svelte:head>

<div class="max-w-md mx-auto px-4 py-20">
    <div
        class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-8 text-center"
    >
        {#if error}
            <h2 class="text-xl font-bold mb-3">Connection Failed</h2>
            <p class="text-base-content/60 mb-6">{error}</p>
            <a href="/strava" class="btn btn-primary rounded-xl mx-auto"
                >Back to Strava</a
            >
        {:else}
            <span class="loading loading-spinner loading-lg mx-auto mb-4"></span>
            <p class="text-base-content/70">{status}</p>
        {/if}
    </div>
</div>
