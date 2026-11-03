<script lang="ts">
    import { onMount } from "svelte";
    import {
        fetchOuraDays,
        score,
        syncOura,
        type OuraDayView,
    } from "$lib/api/oura";
    import { goto } from "$app/navigation";
    import { bootUserDto, fetchBootUserDto } from "$lib/stores/stores";

    const baseUrl = "";

    let historicalData = $state<OuraDayView[]>([]);
    let loading = $state(false);
    let error = $state("");
    let syncing = $state(false);

    // Date range defaults
    const today = new Date();
    const thirtyDaysAgo = new Date(today);
    thirtyDaysAgo.setDate(today.getDate() - 30);

    let startDate = $state(thirtyDaysAgo.toISOString().split("T")[0]);
    let endDate = $state(today.toISOString().split("T")[0]);

    let selectedDataPoint = $state<OuraDayView | null>(null);
    let modalOpen = $state(false);
    let activeTab = $state<"readiness" | "sleep" | "activity" | "resilience">("readiness");

    onMount(async () => {
        await fetchBootUserDto(fetch);
        if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
            goto("/auth");
        } else {
            await loadHistoricalData();
        }
    });

    async function loadHistoricalData() {
        loading = true;
        error = "";
        try {
            const days = await fetchOuraDays(startDate, endDate);

            // The route answers oldest first; this page reads newest first.
            historicalData = days.toSorted(
                (a, b) =>
                    parseLocalDate(b.day).getTime() - parseLocalDate(a.day).getTime(),
            );
        } catch (err) {
            error = "Error loading Oura data";
            console.error(err);
        } finally {
            loading = false;
        }
    }

    function parseLocalDate(dateStr: string | undefined): Date {
        if (!dateStr) return new Date(0);
        const [y, m, d] = dateStr.split("-").map(Number);
        return new Date(y, m - 1, d); // local midnight
    }

    async function syncOuraData() {
        syncing = true;
        error = "";
        try {
            await syncOura(startDate, endDate);
            await loadHistoricalData();
        } catch (err) {
            error = "Error syncing Oura data";
            console.error(err);
        } finally {
            syncing = false;
        }
    }

    function openModal(data: OuraDayView) {
        selectedDataPoint = data;
        modalOpen = true;
    }

    function closeModal() {
        modalOpen = false;
        selectedDataPoint = null;
    }

    function getScoreColor(score: number | null | undefined): string {
        if (!score || score < 0) return "text-base-content/30";
        if (score >= 85) return "text-success";
        if (score >= 70) return "text-warning";
        return "text-error";
    }

    function getProgressColor(score: number | null | undefined): string {
        if (!score || score < 0) return "progress-base-300";
        if (score >= 85) return "progress-success";
        if (score >= 70) return "progress-warning";
        return "progress-error";
    }

    function formatDate(date: any): string {
        if (!date) return "N/A";
        const d = new Date(date.toString());
        return d.toLocaleDateString("en-US", {
            month: "short",
            day: "numeric",
            year: "numeric",
            timeZone: "UTC"
        });
    }
</script>

<svelte:head>
    <title>Oura Data - Historical View</title>
</svelte:head>

<div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
    <!-- Header -->
    <div class="mb-8">
        <h1 class="text-4xl font-display font-black tracking-tight mb-2">
            Oura Data
        </h1>
        <p class="text-base-content/60 text-lg">
            View and analyze your historical Oura Ring metrics
        </p>
    </div>

    <!-- Date Range Selector & Sync -->
    <div
        class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-6 mb-6"
    >
        <div class="flex flex-col sm:flex-row gap-4 items-start sm:items-end">
            <div class="flex-1">
                <label for="start-date" class="label">
                    <span
                        class="label-text font-bold uppercase text-xs tracking-wider"
                        >Start Date</span
                    >
                </label>
                <input
                    id="start-date"
                    type="date"
                    bind:value={startDate}
                    class="input input-bordered w-full rounded-xl"
                    max={endDate}
                />
            </div>

            <div class="flex-1">
                <label for="end-date" class="label">
                    <span
                        class="label-text font-bold uppercase text-xs tracking-wider"
                        >End Date</span
                    >
                </label>
                <input
                    id="end-date"
                    type="date"
                    bind:value={endDate}
                    class="input input-bordered w-full rounded-xl"
                    min={startDate}
                    max={today.toISOString().split("T")[0]}
                />
            </div>

            <div class="flex gap-2">
                <button
                    onclick={loadHistoricalData}
                    disabled={loading}
                    class="btn btn-primary px-5 rounded-xl gap-2"
                >
                    {#if loading}
                        <span class="loading loading-spinner loading-sm"></span>
                    {/if}
                    Load
                </button>

                <button
                    onclick={syncOuraData}
                    disabled={syncing}
                    class="btn btn-accent px-5 rounded-xl gap-2"
                >
                    {#if syncing}
                        <span class="loading loading-spinner loading-sm"></span>
                    {/if}
                    Sync
                </button>
            </div>
        </div>

        {#if error}
            <div class="alert alert-error mt-4 rounded-xl">
                <svg
                    xmlns="http://www.w3.org/2000/svg"
                    class="h-6 w-6 shrink-0 stroke-current"
                    fill="none"
                    viewBox="0 0 24 24"
                >
                    <path
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="2"
                        d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z"
                    />
                </svg>
                <span>{error}</span>
            </div>
        {/if}
    </div>

    <!-- Historical Data Grid -->
    {#if loading}
        <div class="flex justify-center items-center py-20">
            <span class="loading loading-spinner loading-lg"></span>
        </div>
    {:else if historicalData.length === 0}
        <div
            class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-12 text-center"
        >
            <svg
                xmlns="http://www.w3.org/2000/svg"
                fill="none"
                viewBox="0 0 24 24"
                stroke-width="1.5"
                stroke="currentColor"
                class="w-16 h-16 mx-auto mb-4 text-base-content/30"
            >
                <path
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    d="M20.25 6.375c0 2.278-3.694 4.125-8.25 4.125S3.75 8.653 3.75 6.375m16.5 0c0-2.278-3.694-4.125-8.25-4.125S3.75 4.097 3.75 6.375m16.5 0v11.25c0 2.278-3.694 4.125-8.25 4.125s-8.25-1.847-8.25-4.125V6.375m16.5 0v3.75m-16.5-3.75v3.75m16.5 0v3.75C20.25 16.153 16.556 18 12 18s-8.25-1.847-8.25-4.125v-3.75m16.5 0c0 2.278-3.694 4.125-8.25 4.125s-8.25-1.847-8.25-4.125"
                />
            </svg>
            <h3 class="text-xl font-bold mb-2">No Data Available</h3>
            <p class="text-base-content/60 mb-4">
                No Oura data found for the selected date range.
            </p>
            <button
                onclick={syncOuraData}
                class="btn btn-primary px-6 rounded-xl mx-auto"
            >
                Sync Data Now
            </button>
        </div>
    {:else}
        <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {#each historicalData as data}
                <button
                    onclick={() => openModal(data)}
                    class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
                 cursor-pointer p-6 text-left w-full
                 border-2 border-base-content/10 hover:border-base-content/30"
                >
                    <!-- Date Header -->
                    <div class="flex items-start justify-between mb-4">
                        <div>
                            <span
                                class="text-xs font-bold uppercase tracking-widest text-base-content/50"
                            >
                                {data.day}
                            </span>
                        </div>
                        <svg
                            xmlns="http://www.w3.org/2000/svg"
                            fill="none"
                            viewBox="0 0 24 24"
                            stroke-width="2"
                            stroke="currentColor"
                            class="w-5 h-5 text-base-content/30"
                        >
                            <path
                                stroke-linecap="round"
                                stroke-linejoin="round"
                                d="m8.25 4.5 7.5 7.5-7.5 7.5"
                            />
                        </svg>
                    </div>

                    <!-- Three Score Displays -->
                    <div class="grid grid-cols-3 gap-3">
                        <div class="flex flex-col gap-1">
                            <span
                                class="text-xs font-bold uppercase tracking-wider text-success/70"
                                >Ready</span
                            >
                            <span
                                class="text-3xl font-display font-black {getScoreColor(
                                    data.readiness?.score,
                                )} leading-none"
                            >
                                {data.readiness?.score ?? "-"}
                            </span>
                        </div>
                        <div class="flex flex-col gap-1">
                            <span
                                class="text-xs font-bold uppercase tracking-wider text-info/70"
                                >Sleep</span
                            >
                            <span
                                class="text-3xl font-display font-black {getScoreColor(
                                    data.sleep?.score,
                                )} leading-none"
                            >
                                {data.sleep?.score ?? "-"}
                            </span>
                        </div>
                        <div class="flex flex-col gap-1">
                            <span
                                class="text-xs font-bold uppercase tracking-wider text-warning/70"
                                >Active</span
                            >
                            <span
                                class="text-3xl font-display font-black {getScoreColor(
                                    data.activity?.score,
                                )} leading-none"
                            >
                                {data.activity?.score ?? "-"}
                            </span>
                        </div>
                    </div>

                    <!-- Quick Stats -->
                    <div
                        class="mt-4 pt-4 border-t border-base-200 flex justify-between text-xs"
                    >
                        <div>
                            <span class="text-base-content/50">Steps:</span>
                            <span class="font-bold ml-1"
                                >{data.activity?.steps?.toLocaleString() ??
                                    "-"}</span
                            >
                        </div>
                        <div>
                            <span class="text-base-content/50">Temp:</span>
                            <span class="font-bold ml-1"
                                >{data.readiness?.temperatureDeviation ??
                                    "-"}°</span
                            >
                        </div>
                    </div>
                </button>
            {/each}
        </div>
    {/if}
</div>

<!-- Detail Modal -->
{#if selectedDataPoint}
    <dialog class="modal" class:modal-open={modalOpen}>
        <div
            class="modal-box rounded-3xl max-w-2xl bg-base-100 p-0 overflow-hidden max-h-[90vh]"
        >
            <!-- Header -->
            <div class="p-6 pb-4 border-b border-base-200">
                <div class="flex items-center justify-between">
                    <div class="flex items-center gap-3">
                        <div
                            class="w-12 h-12 rounded-2xl bg-accent/10 flex items-center justify-center"
                        >
                            <svg
                                xmlns="http://www.w3.org/2000/svg"
                                fill="none"
                                viewBox="0 0 24 24"
                                stroke-width="1.5"
                                stroke="currentColor"
                                class="w-6 h-6 text-accent"
                            >
                                <path
                                    stroke-linecap="round"
                                    stroke-linejoin="round"
                                    d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z"
                                />
                            </svg>
                        </div>
                        <div>
                            <h3 class="font-semibold text-lg">Oura Metrics</h3>
                            <p class="text-sm text-base-content/50">
                                {formatDate(selectedDataPoint?.day)}
                            </p>
                        </div>
                    </div>
                    <button
                        onclick={closeModal}
                        aria-label="Close"
                        class="btn btn-ghost btn-circle h-11 min-h-11 w-11"
                    >
                        <svg
                            xmlns="http://www.w3.org/2000/svg"
                            fill="none"
                            viewBox="0 0 24 24"
                            stroke-width="1.5"
                            stroke="currentColor"
                            class="w-5 h-5"
                        >
                            <path
                                stroke-linecap="round"
                                stroke-linejoin="round"
                                d="M6 18 18 6M6 6l12 12"
                            />
                        </svg>
                    </button>
                </div>
            </div>

            <!-- Tabs -->
            <div class="px-6 pt-4">
                <div class="flex gap-2 p-1 bg-base-200 rounded-2xl">
                    <button
                        onclick={() => (activeTab = "readiness")}
                        class="flex-1 py-2 px-4 rounded-xl text-sm font-medium transition-all duration-200
                   {activeTab === 'readiness'
                            ? 'bg-base-100 shadow-xs'
                            : 'hover:bg-base-300'}"
                    >
                        Readiness
                    </button>
                    <button
                        onclick={() => (activeTab = "sleep")}
                        class="flex-1 py-2 px-4 rounded-xl text-sm font-medium transition-all duration-200
                   {activeTab === 'sleep'
                            ? 'bg-base-100 shadow-xs'
                            : 'hover:bg-base-300'}"
                    >
                        Sleep
                    </button>
                    <button
                        onclick={() => (activeTab = "activity")}
                        class="flex-1 py-2 px-4 rounded-xl text-sm font-medium transition-all duration-200
                   {activeTab === 'activity'
                            ? 'bg-base-100 shadow-xs'
                            : 'hover:bg-base-300'}"
                    >
                        Activity
                    </button>
                    <button
                        onclick={() => (activeTab = "resilience")}
                        class="flex-1 py-2 px-4 rounded-xl text-sm font-medium transition-all duration-200
                   {activeTab === 'resilience'
                            ? 'bg-base-100 shadow-xs'
                            : 'hover:bg-base-300'}"
                    >
                        Resilience
                    </button>
                </div>
            </div>

            <!-- Content -->
            <div class="p-6 overflow-y-auto max-h-[60vh]">
                {#if activeTab === "readiness"}
                    <div class="text-center mb-6">
                        <p
                            class="text-6xl font-bold {getScoreColor(
                                selectedDataPoint?.readiness?.score,
                            )}"
                        >
                            {selectedDataPoint?.readiness?.score ??
                                "-"}
                        </p>
                        <p class="text-base-content/50 mt-1">Readiness Score</p>
                    </div>
                    <div class="space-y-4">
                        {#each [{ label: "Sleep Balance", value: selectedDataPoint?.readiness?.contributors?.sleepBalance }, { label: "Activity Balance", value: selectedDataPoint?.readiness?.contributors?.activityBalance }, { label: "Recovery Index", value: selectedDataPoint?.readiness?.contributors?.recoveryIndex }, { label: "HRV Balance", value: selectedDataPoint?.readiness?.contributors?.hrvBalance }, { label: "Previous Night", value: selectedDataPoint?.readiness?.contributors?.previousNight }, { label: "Previous Day Activity", value: selectedDataPoint?.readiness?.contributors?.previousDayActivity }, { label: "Resting Heart Rate", value: selectedDataPoint?.readiness?.contributors?.restingHeartRate }] as item}
                            <div class="flex items-center justify-between">
                                <span class="text-sm">{item.label}</span>
                                <div class="flex items-center gap-3 w-1/2">
                                    <progress
                                        class="progress {getProgressColor(
                                            item.value,
                                        )} flex-1 h-2"
                                        value={item.value ?? 0}
                                        max="100"
                                    ></progress>
                                    <span
                                        class="text-sm font-medium w-8 text-right"
                                        >{item.value ?? "-"}</span
                                    >
                                </div>
                            </div>
                        {/each}
                        <div
                            class="flex items-center justify-between pt-2 border-t border-base-200"
                        >
                            <span class="text-sm">Temperature Deviation</span>
                            <span class="font-medium"
                                >{selectedDataPoint?.readiness
                                    ?.temperatureDeviation ?? "-"}°</span
                            >
                        </div>
                    </div>
                {:else if activeTab === "sleep"}
                    <div class="text-center mb-6">
                        <p
                            class="text-6xl font-bold {getScoreColor(
                                selectedDataPoint?.sleep?.score,
                            )}"
                        >
                            {selectedDataPoint?.sleep?.score ?? "-"}
                        </p>
                        <p class="text-base-content/50 mt-1">Sleep Score</p>
                    </div>
                    <div class="space-y-4">
                        {#each [{ label: "Total Sleep", value: selectedDataPoint?.sleep?.contributors?.totalSleep }, { label: "Efficiency", value: selectedDataPoint?.sleep?.contributors?.efficiency }, { label: "Restfulness", value: selectedDataPoint?.sleep?.contributors?.restfulness }, { label: "Timing", value: selectedDataPoint?.sleep?.contributors?.timing }, { label: "Latency", value: selectedDataPoint?.sleep?.contributors?.latency }, { label: "REM Sleep", value: selectedDataPoint?.sleep?.contributors?.remSleep }, { label: "Deep Sleep", value: selectedDataPoint?.sleep?.contributors?.deepSleep }] as item}
                            <div class="flex items-center justify-between">
                                <span class="text-sm">{item.label}</span>
                                <div class="flex items-center gap-3 w-1/2">
                                    <progress
                                        class="progress {getProgressColor(
                                            item.value,
                                        )} flex-1 h-2"
                                        value={item.value ?? 0}
                                        max="100"
                                    ></progress>
                                    <span
                                        class="text-sm font-medium w-8 text-right"
                                        >{item.value ?? "-"}</span
                                    >
                                </div>
                            </div>
                        {/each}
                    </div>
                {:else if activeTab === "activity"}
                    <div class="text-center mb-6">
                        <p
                            class="text-6xl font-bold {getScoreColor(
                                selectedDataPoint?.activity?.score,
                            )}"
                        >
                            {selectedDataPoint?.activity?.score ??
                                "-"}
                        </p>
                        <p class="text-base-content/50 mt-1">Activity Score</p>
                    </div>

                    <div class="grid grid-cols-3 gap-4 mb-6">
                        <div class="bg-base-200 rounded-2xl p-4 text-center">
                            <p class="text-2xl font-bold">
                                {selectedDataPoint?.activity?.steps?.toLocaleString() ??
                                    "-"}
                            </p>
                            <p class="text-xs text-base-content/50">Steps</p>
                        </div>
                        <div class="bg-base-200 rounded-2xl p-4 text-center">
                            <p class="text-2xl font-bold">
                                {selectedDataPoint?.activity
                                    ?.activeCalories ?? "-"}
                            </p>
                            <p class="text-xs text-base-content/50">
                                Active Cal
                            </p>
                        </div>
                        <div class="bg-base-200 rounded-2xl p-4 text-center">
                            <p class="text-2xl font-bold">
                                {selectedDataPoint?.activity
                                    ?.totalCalories ?? "-"}
                            </p>
                            <p class="text-xs text-base-content/50">
                                Total Cal
                            </p>
                        </div>
                    </div>

                    <div class="space-y-4">
                        {#each [{ label: "Training Frequency", value: selectedDataPoint?.activity?.contributors?.trainingFrequency }, { label: "Training Volume", value: selectedDataPoint?.activity?.contributors?.trainingVolume }, { label: "Recovery Time", value: selectedDataPoint?.activity?.contributors?.recoveryTime }, { label: "Stay Active", value: selectedDataPoint?.activity?.contributors?.stayActive }, { label: "Move Every Hour", value: selectedDataPoint?.activity?.contributors?.moveEveryHour }, { label: "Meet Daily Targets", value: selectedDataPoint?.activity?.contributors?.meetDailyTargets }] as item}
                            <div class="flex items-center justify-between">
                                <span class="text-sm">{item.label}</span>
                                <div class="flex items-center gap-3 w-1/2">
                                    <progress
                                        class="progress {getProgressColor(
                                            item.value,
                                        )} flex-1 h-2"
                                        value={item.value ?? 0}
                                        max="100"
                                    ></progress>
                                    <span
                                        class="text-sm font-medium w-8 text-right"
                                        >{item.value ?? "-"}</span
                                    >
                                </div>
                            </div>
                        {/each}
                    </div>
                {:else if activeTab === "resilience"}
                    <div class="text-center mb-6">
                        <p class="text-4xl font-bold text-accent">
                            {selectedDataPoint?.resilience
                                ?.level ?? "-"}
                        </p>
                        <p class="text-base-content/50 mt-1">
                            Resilience Level
                        </p>
                    </div>
                    <div class="space-y-4">
                        {#each [{ label: "Sleep Recovery", value: selectedDataPoint?.resilience?.contributors?.sleepRecovery }, { label: "Daytime Recovery", value: selectedDataPoint?.resilience?.contributors?.daytimeRecovery }, { label: "Stress", value: selectedDataPoint?.resilience?.contributors?.stress }] as item}
                            <div class="flex items-center justify-between">
                                <span class="text-sm">{item.label}</span>
                                <div class="flex items-center gap-3 w-1/2">
                                    <progress
                                        class="progress {getProgressColor(
                                            item.value,
                                        )} flex-1 h-2"
                                        value={item.value ?? 0}
                                        max="100"
                                    ></progress>
                                    <span
                                        class="text-sm font-medium w-8 text-right"
                                        >{item.value ?? "-"}</span
                                    >
                                </div>
                            </div>
                        {/each}
                    </div>
                {/if}
            </div>
        </div>
        <form
            method="dialog"
            class="modal-backdrop bg-black/50 backdrop-blur-xs"
        >
            <button onclick={closeModal}>close</button>
        </form>
    </dialog>
{/if}
