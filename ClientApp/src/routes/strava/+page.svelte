<script lang="ts">
    import { onMount } from "svelte";
    import { goto } from "$app/navigation";
    import { bootUserDto, fetchBootUserDto } from "$lib/stores/stores";

    interface StravaActivity {
        objectID: string;
        stravaActivityID: number;
        name: string;
        sportType: string;
        startDate: string;
        startDateLocal: string;
        elapsedTimeSeconds: number;
        movingTimeSeconds: number;
        distanceMeters: number;
        totalElevationGainMeters: number;
        averageSpeed: number;
        maxSpeed: number;
        averageHeartrate: number | null;
        maxHeartrate: number | null;
    }

    let connected = false;
    let lastSyncedAt: string | null = null;
    let activities: StravaActivity[] = [];

    let loading = false;
    let syncing = false;
    let connecting = false;
    let error = "";
    let showReconnectModal = false;
    // true when the existing authorization is dead (reauth); false when never connected.
    let modalIsReauth = false;

    // Default to the past year so the initial backfill is visible.
    const today = new Date();
    const oneYearAgo = new Date(today);
    oneYearAgo.setFullYear(today.getFullYear() - 1);
    let startDate = oneYearAgo.toISOString().split("T")[0];
    let endDate = today.toISOString().split("T")[0];

    onMount(async () => {
        await fetchBootUserDto(fetch);
        if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
            goto("/auth");
            return;
        }
        await loadStatus();
        if (connected) {
            await loadActivities();
        }
    });

    async function loadStatus() {
        try {
            const response = await fetch("/api/strava/status");
            if (response.ok) {
                const status = await response.json();
                connected = status.connected;
                lastSyncedAt = status.lastSyncedAt;
            }
        } catch (err) {
            console.error("Error loading Strava status", err);
        }
    }

    async function loadActivities() {
        loading = true;
        error = "";
        try {
            const response = await fetch("/api/strava/get-activities", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ startDate, endDate }),
            });
            if (response.ok) {
                activities = await response.json();
            } else {
                error = "Failed to load Strava activities";
            }
        } catch (err) {
            error = "Error loading Strava activities";
            console.error(err);
        } finally {
            loading = false;
        }
    }

    async function connectStrava() {
        connecting = true;
        error = "";
        try {
            const response = await fetch("/api/strava/auth-url");
            if (response.ok) {
                const { url } = await response.json();
                window.location.href = url;
            } else {
                error =
                    "Strava is not configured. Please contact the administrator.";
                connecting = false;
            }
        } catch (err) {
            error = "Error starting Strava connection";
            console.error(err);
            connecting = false;
        }
    }

    async function syncStrava() {
        syncing = true;
        error = "";
        try {
            const response = await fetch("/api/strava/sync", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
            });
            if (response.ok) {
                const result = await response.json();
                if (!result.connected || result.reauthRequired) {
                    // Credentials missing or could not be refreshed -> prompt (re)connect.
                    connected = result.connected;
                    modalIsReauth = result.reauthRequired;
                    showReconnectModal = true;
                } else {
                    await loadStatus();
                    await loadActivities();
                }
            } else {
                error = "Failed to sync Strava activities";
            }
        } catch (err) {
            error = "Error syncing Strava activities";
            console.error(err);
        } finally {
            syncing = false;
        }
    }

    function formatDate(value: string | null): string {
        if (!value) return "Never";
        return new Date(value).toLocaleString("en-US", {
            month: "short",
            day: "numeric",
            year: "numeric",
            hour: "numeric",
            minute: "2-digit",
        });
    }

    function formatDay(value: string): string {
        return new Date(value).toLocaleDateString("en-US", {
            month: "short",
            day: "numeric",
            year: "numeric",
        });
    }

    function formatDistance(meters: number): string {
        return (meters / 1000).toFixed(2) + " km";
    }

    function formatDuration(seconds: number): string {
        const h = Math.floor(seconds / 3600);
        const m = Math.floor((seconds % 3600) / 60);
        const s = seconds % 60;
        if (h > 0) return `${h}h ${m}m`;
        if (m > 0) return `${m}m ${s}s`;
        return `${s}s`;
    }
</script>

<svelte:head>
    <title>Strava</title>
</svelte:head>

<div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
    <!-- Header -->
    <div class="mb-8">
        <h1 class="text-4xl font-display font-black tracking-tight mb-2">
            Strava
        </h1>
        <p class="text-base-content/60 text-lg">
            Connect your Strava account and sync your activities
        </p>
    </div>

    <!-- Connection / Sync Card -->
    <div
        class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-6 mb-6"
    >
        {#if connected}
            <div
                class="flex flex-col sm:flex-row gap-4 items-start sm:items-center justify-between"
            >
                <div class="flex items-center gap-3">
                    <div
                        class="w-3 h-3 rounded-full bg-success"
                        title="Connected"
                    ></div>
                    <div>
                        <p class="font-bold uppercase tracking-wider text-sm">
                            Connected to Strava
                        </p>
                        <p class="text-xs text-base-content/60">
                            Last synced: {formatDate(lastSyncedAt)}
                        </p>
                    </div>
                </div>
                <button
                    on:click={syncStrava}
                    disabled={syncing}
                    class="btn btn-accent px-5 rounded-xl gap-2"
                >
                    {#if syncing}
                        <span class="loading loading-spinner loading-sm"></span>
                    {/if}
                    Sync
                </button>
            </div>
        {:else}
            <div
                class="flex flex-col sm:flex-row gap-4 items-start sm:items-center justify-between"
            >
                <div class="flex items-center gap-3">
                    <div class="w-3 h-3 rounded-full bg-base-content/30"></div>
                    <div>
                        <p class="font-bold uppercase tracking-wider text-sm">
                            Not Connected
                        </p>
                        <p class="text-xs text-base-content/60">
                            Connect your Strava account to import activities
                        </p>
                    </div>
                </div>
                <button
                    on:click={connectStrava}
                    disabled={connecting}
                    class="btn btn-primary px-5 rounded-xl gap-2"
                >
                    {#if connecting}
                        <span class="loading loading-spinner loading-sm"></span>
                    {/if}
                    Connect Strava
                </button>
            </div>
        {/if}

        {#if error}
            <div class="alert alert-error mt-4 rounded-xl">
                <span>{error}</span>
            </div>
        {/if}
    </div>

    <!-- Activities -->
    {#if connected}
        {#if loading}
            <div class="flex justify-center items-center py-20">
                <span class="loading loading-spinner loading-lg"></span>
            </div>
        {:else if activities.length === 0}
            <div
                class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-12 text-center"
            >
                <h3 class="text-xl font-bold mb-2">No Activities Yet</h3>
                <p class="text-base-content/60 mb-4">
                    Hit Sync to import your Strava activities.
                </p>
                <button
                    on:click={syncStrava}
                    disabled={syncing}
                    class="btn btn-primary px-6 rounded-xl mx-auto"
                >
                    Sync Now
                </button>
            </div>
        {:else}
            <div
                class="card bg-base-100 shadow-editorial border-2 border-base-content/10 overflow-hidden"
            >
                <div class="overflow-x-auto">
                    <table class="table">
                        <thead>
                            <tr>
                                <th>Date</th>
                                <th>Name</th>
                                <th>Sport</th>
                                <th class="text-right">Distance</th>
                                <th class="text-right">Moving</th>
                                <th class="text-right">Avg HR</th>
                            </tr>
                        </thead>
                        <tbody>
                            {#each activities as activity}
                                <tr>
                                    <td class="whitespace-nowrap"
                                        >{formatDay(activity.startDateLocal)}</td
                                    >
                                    <td class="font-bold">{activity.name}</td>
                                    <td>
                                        <span class="badge badge-ghost"
                                            >{activity.sportType}</span
                                        >
                                    </td>
                                    <td class="text-right whitespace-nowrap"
                                        >{formatDistance(
                                            activity.distanceMeters,
                                        )}</td
                                    >
                                    <td class="text-right whitespace-nowrap"
                                        >{formatDuration(
                                            activity.movingTimeSeconds,
                                        )}</td
                                    >
                                    <td class="text-right whitespace-nowrap">
                                        {activity.averageHeartrate
                                            ? Math.round(
                                                  activity.averageHeartrate,
                                              ) + " bpm"
                                            : "-"}
                                    </td>
                                </tr>
                            {/each}
                        </tbody>
                    </table>
                </div>
            </div>
        {/if}
    {/if}
</div>

<!-- Reconnect Modal -->
{#if showReconnectModal}
    <div class="modal modal-open">
        <div class="modal-box w-11/12 max-w-md">
            <button
                class="btn btn-sm btn-circle btn-ghost absolute right-2 top-2"
                on:click={() => (showReconnectModal = false)}>✕</button
            >
            <h3 class="text-xl font-bold uppercase tracking-wider mb-2">
                {modalIsReauth ? "Reconnect Strava" : "Connect Strava"}
            </h3>
            <p
                class="text-sm text-base-content/70 mb-6"
            >
                {modalIsReauth
                    ? "Your Strava authorization has expired or was revoked. Reconnect your account to continue syncing activities."
                    : "Connect your Strava account to import your activities."}
            </p>
            <div class="modal-action">
                <button
                    type="button"
                    class="btn btn-outline px-5 rounded-xl"
                    on:click={() => (showReconnectModal = false)}>Cancel</button
                >
                <button
                    type="button"
                    class="btn btn-primary px-5 rounded-xl gap-2"
                    disabled={connecting}
                    on:click={connectStrava}
                >
                    {#if connecting}
                        <span class="loading loading-spinner loading-sm"></span>
                    {/if}
                    {modalIsReauth ? "Reconnect" : "Connect"}
                </button>
            </div>
        </div>
        <div
            class="modal-backdrop bg-base-300/80"
            role="button"
            tabindex="0"
            aria-label="Close modal"
            on:click={() => (showReconnectModal = false)}
            on:keydown={(e) =>
                (e.key === "Escape" || e.key === "Enter" || e.key === " ") &&
                (showReconnectModal = false)}
        ></div>
    </div>
{/if}
