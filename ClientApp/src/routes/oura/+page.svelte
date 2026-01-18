<script lang="ts">
    import { onMount } from "svelte";
    import type { DailyOuraDataDTO } from "$lib/api/ApiClient";
    import { goto } from "$app/navigation";
    import { bootUserDto, fetchBootUserDto } from "$lib/stores/stores";

    const baseUrl = "";

    let historicalData: DailyOuraDataDTO[] = [];
    let loading = false;
    let error = "";
    let syncing = false;

    // Date range defaults
    const today = new Date();
    const thirtyDaysAgo = new Date(today);
    thirtyDaysAgo.setDate(today.getDate() - 30);

    let startDate = thirtyDaysAgo.toISOString().split("T")[0];
    let endDate = today.toISOString().split("T")[0];

    let selectedDataPoint: DailyOuraDataDTO | null = null;
    let modalOpen = false;
    let activeTab: "readiness" | "sleep" | "activity" | "resilience" =
        "readiness";

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
            const response = await fetch(
                "/api/oura/get-daily-oura-data-range",
                {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                    },
                    body: JSON.stringify({
                        startDate,
                        endDate,
                    }),
                },
            );

            if (response.ok) {
                historicalData = await response.json();

                historicalData.sort((a, b) => {
                    const dateA = parseLocalDate(a.date?.toString());
                    const dateB = parseLocalDate(b.date?.toString());
                    return dateB.getTime() - dateA.getTime();
                });
                console.log(historicalData);
            } else {
                error = "Failed to load Oura data";
            }
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
            const response = await fetch("/api/oura/sync", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify({
                    startDate,
                    endDate,
                }),
            });

            if (response.ok) {
                await loadHistoricalData();
            } else {
                error = "Failed to sync Oura data";
            }
        } catch (err) {
            error = "Error syncing Oura data";
            console.error(err);
        } finally {
            syncing = false;
        }
    }

    function openModal(data: DailyOuraDataDTO) {
        selectedDataPoint = data;
        modalOpen = true;
    }

    function closeModal() {
        modalOpen = false;
        selectedDataPoint = null;
    }

    function getScoreColor(score: number | undefined): string {
        if (!score || score < 0) return "text-base-content/30";
        if (score >= 85) return "text-success";
        if (score >= 70) return "text-warning";
        return "text-error";
    }

    function getProgressColor(score: number | undefined): string {
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
                    on:click={loadHistoricalData}
                    disabled={loading}
                    class="btn btn-primary p-2 rounded-xl font-bold uppercase"
                >
                    {#if loading}
                        <span class="loading loading-spinner loading-sm"></span>
                    {/if}
                    Load
                </button>

                <button
                    on:click={syncOuraData}
                    disabled={syncing}
                    class="btn btn-accent rounded-xl font-bold uppercase tracking-wider"
                >
                    {#if syncing}
                        <span class="loading loading-spinner p-2 loading-sm"
                        ></span>
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
                on:click={syncOuraData}
                class="btn btn-primary rounded-xl font-bold uppercase tracking-wider mx-auto"
            >
                Sync Data Now
            </button>
        </div>
    {:else}
        <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {#each historicalData as data}
                <button
                    on:click={() => openModal(data)}
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
                                {(data.date)}
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
                                    data.readinessData?.readinessScore,
                                )} leading-none"
                            >
                                {data.readinessData?.readinessScore ?? "-"}
                            </span>
                        </div>
                        <div class="flex flex-col gap-1">
                            <span
                                class="text-xs font-bold uppercase tracking-wider text-info/70"
                                >Sleep</span
                            >
                            <span
                                class="text-3xl font-display font-black {getScoreColor(
                                    data.sleepData?.sleepScore,
                                )} leading-none"
                            >
                                {data.sleepData?.sleepScore ?? "-"}
                            </span>
                        </div>
                        <div class="flex flex-col gap-1">
                            <span
                                class="text-xs font-bold uppercase tracking-wider text-warning/70"
                                >Active</span
                            >
                            <span
                                class="text-3xl font-display font-black {getScoreColor(
                                    data.activityData?.activityScore,
                                )} leading-none"
                            >
                                {data.activityData?.activityScore ?? "-"}
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
                                >{data.activityData?.steps?.toLocaleString() ??
                                    "-"}</span
                            >
                        </div>
                        <div>
                            <span class="text-base-content/50">Temp:</span>
                            <span class="font-bold ml-1"
                                >{data.readinessData?.temperatureDeviation ??
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
                                {formatDate(selectedDataPoint.date)}
                            </p>
                        </div>
                    </div>
                    <button
                        on:click={closeModal}
                        class="btn btn-ghost btn-sm btn-circle"
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
                        on:click={() => (activeTab = "readiness")}
                        class="flex-1 py-2 px-4 rounded-xl text-sm font-medium transition-all duration-200
                   {activeTab === 'readiness'
                            ? 'bg-base-100 shadow-sm'
                            : 'hover:bg-base-300'}"
                    >
                        Readiness
                    </button>
                    <button
                        on:click={() => (activeTab = "sleep")}
                        class="flex-1 py-2 px-4 rounded-xl text-sm font-medium transition-all duration-200
                   {activeTab === 'sleep'
                            ? 'bg-base-100 shadow-sm'
                            : 'hover:bg-base-300'}"
                    >
                        Sleep
                    </button>
                    <button
                        on:click={() => (activeTab = "activity")}
                        class="flex-1 py-2 px-4 rounded-xl text-sm font-medium transition-all duration-200
                   {activeTab === 'activity'
                            ? 'bg-base-100 shadow-sm'
                            : 'hover:bg-base-300'}"
                    >
                        Activity
                    </button>
                    <button
                        on:click={() => (activeTab = "resilience")}
                        class="flex-1 py-2 px-4 rounded-xl text-sm font-medium transition-all duration-200
                   {activeTab === 'resilience'
                            ? 'bg-base-100 shadow-sm'
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
                                selectedDataPoint.readinessData?.readinessScore,
                            )}"
                        >
                            {selectedDataPoint.readinessData?.readinessScore ??
                                "-"}
                        </p>
                        <p class="text-base-content/50 mt-1">Readiness Score</p>
                    </div>
                    <div class="space-y-4">
                        {#each [{ label: "Sleep Balance", value: selectedDataPoint.readinessData?.sleepBalance }, { label: "Activity Balance", value: selectedDataPoint.readinessData?.activityBalance }, { label: "Recovery Index", value: selectedDataPoint.readinessData?.recoveryIndex }, { label: "HRV Balance", value: selectedDataPoint.readinessData?.hrvBalance }, { label: "Previous Night", value: selectedDataPoint.readinessData?.previousNight }, { label: "Previous Day Activity", value: selectedDataPoint.readinessData?.previousDayActivity }, { label: "Resting Heart Rate", value: selectedDataPoint.readinessData?.restingHeartRate }] as item}
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
                                >{selectedDataPoint.readinessData
                                    ?.temperatureDeviation ?? "-"}°</span
                            >
                        </div>
                    </div>
                {:else if activeTab === "sleep"}
                    <div class="text-center mb-6">
                        <p
                            class="text-6xl font-bold {getScoreColor(
                                selectedDataPoint.sleepData?.sleepScore,
                            )}"
                        >
                            {selectedDataPoint.sleepData?.sleepScore ?? "-"}
                        </p>
                        <p class="text-base-content/50 mt-1">Sleep Score</p>
                    </div>
                    <div class="space-y-4">
                        {#each [{ label: "Total Sleep", value: selectedDataPoint.sleepData?.totalSleep }, { label: "Efficiency", value: selectedDataPoint.sleepData?.efficiency }, { label: "Restfulness", value: selectedDataPoint.sleepData?.restfulness }, { label: "Timing", value: selectedDataPoint.sleepData?.timing }, { label: "Latency", value: selectedDataPoint.sleepData?.latency }, { label: "REM Sleep", value: selectedDataPoint.sleepData?.remSleep }, { label: "Deep Sleep", value: selectedDataPoint.sleepData?.deepSleep }] as item}
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
                                selectedDataPoint.activityData?.activityScore,
                            )}"
                        >
                            {selectedDataPoint.activityData?.activityScore ??
                                "-"}
                        </p>
                        <p class="text-base-content/50 mt-1">Activity Score</p>
                    </div>

                    <div class="grid grid-cols-3 gap-4 mb-6">
                        <div class="bg-base-200 rounded-2xl p-4 text-center">
                            <p class="text-2xl font-bold">
                                {selectedDataPoint.activityData?.steps?.toLocaleString() ??
                                    "-"}
                            </p>
                            <p class="text-xs text-base-content/50">Steps</p>
                        </div>
                        <div class="bg-base-200 rounded-2xl p-4 text-center">
                            <p class="text-2xl font-bold">
                                {selectedDataPoint.activityData
                                    ?.activeCalories ?? "-"}
                            </p>
                            <p class="text-xs text-base-content/50">
                                Active Cal
                            </p>
                        </div>
                        <div class="bg-base-200 rounded-2xl p-4 text-center">
                            <p class="text-2xl font-bold">
                                {selectedDataPoint.activityData
                                    ?.totalCalories ?? "-"}
                            </p>
                            <p class="text-xs text-base-content/50">
                                Total Cal
                            </p>
                        </div>
                    </div>

                    <div class="space-y-4">
                        {#each [{ label: "Training Frequency", value: selectedDataPoint.activityData?.trainingFrequency }, { label: "Training Volume", value: selectedDataPoint.activityData?.trainingVolume }, { label: "Recovery Time", value: selectedDataPoint.activityData?.recoveryTime }, { label: "Stay Active", value: selectedDataPoint.activityData?.stayActive }, { label: "Move Every Hour", value: selectedDataPoint.activityData?.moveEveryHour }, { label: "Meet Daily Targets", value: selectedDataPoint.activityData?.meetDailyTargets }] as item}
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
                            {selectedDataPoint.resilienceData
                                ?.resilienceLevel ?? "-"}
                        </p>
                        <p class="text-base-content/50 mt-1">
                            Resilience Level
                        </p>
                    </div>
                    <div class="space-y-4">
                        {#each [{ label: "Sleep Recovery", value: selectedDataPoint.resilienceData?.sleepRecovery }, { label: "Daytime Recovery", value: selectedDataPoint.resilienceData?.daytimeRecovery }, { label: "Stress", value: selectedDataPoint.resilienceData?.stress }] as item}
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
            class="modal-backdrop bg-black/50 backdrop-blur-sm"
        >
            <button on:click={closeModal}>close</button>
        </form>
    </dialog>
{/if}
