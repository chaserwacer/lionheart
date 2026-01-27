<script lang="ts">
    import { onMount } from "svelte";
    import { goto } from "$app/navigation";
    import { bootUserDto, fetchBootUserDto, pageUpdate } from "$lib/stores/stores";
    import {
        GetWellnessStatesEndpointClient,
        GetWellnessStateEndpointClient,
        AddWellnessStateEndpointClient,
        CreateWellnessStateRequest,
        type WellnessState
    } from "$lib/api/ApiClient";

    const baseUrl = "";
    
    const getWellnessStatesClient = new GetWellnessStatesEndpointClient(baseUrl);
    const getWellnessStateClient = new GetWellnessStateEndpointClient(baseUrl);
    const addWellnessStateClient = new AddWellnessStateEndpointClient(baseUrl);

    let wellnessStates: WellnessState[] = [];
    let todaysWellness: WellnessState | null = null;
    let loading = false;
    let error = "";
    
    let startDate = new Date(new Date().setDate(new Date().getDate() - 30)).toISOString().split("T")[0];
    let endDate = new Date().toISOString().split("T")[0];
    
    let logWellnessModalOpen = false;
    let viewWellnessModalOpen = false;
    let selectedWellness: WellnessState | null = null;
    
    let wellnessForm = {
        date: new Date().toISOString().split("T")[0],
        energy: 3,
        motivation: 3,
        mood: 3,
        stress: 3
    };

    let sortDirection: "asc" | "desc" = "desc";

    // Parse date string as local date (not UTC)
    function parseLocalDate(dateStr: string): Date {
        const [year, month, day] = dateStr.split('-').map(Number);
        return new Date(year, month - 1, day);
    }

    onMount(async () => {
        await fetchBootUserDto(fetch);
        if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
            goto("/auth");
        } else {
            await Promise.all([loadWellnessStates(), loadTodaysWellness()]);
        }
    });

    async function loadWellnessStates() {
        loading = true;
        error = "";
        try {
            wellnessStates = await getWellnessStatesClient.get(parseLocalDate(startDate), parseLocalDate(endDate));
        } catch (err) {
            error = "Failed to load wellness data";
            console.error(err);
        } finally {
            loading = false;
        } 
    }

    async function loadTodaysWellness() {
        try {
            todaysWellness = await getWellnessStateClient.get(new Date(new Date().toDateString()));
        } catch (err) {
            todaysWellness = null;
        }
    }

    async function logWellness() {
        try {
            const request = new CreateWellnessStateRequest({
                date: parseLocalDate(wellnessForm.date),
                energy: wellnessForm.energy,
                motivation: wellnessForm.motivation,
                mood: wellnessForm.mood,
                stress: wellnessForm.stress
            });
            
            await addWellnessStateClient.post(request);
            await Promise.all([loadWellnessStates(), loadTodaysWellness()]);
            closeLogWellnessModal();
            resetWellnessForm();
            pageUpdate.set(new Date());
        } catch (err) {
            error = "Failed to log wellness";
            console.error(err);
        }
    }

    function openLogWellnessModal(forDate?: string) {
        wellnessForm.date = forDate ?? new Date().toISOString().split("T")[0];
        logWellnessModalOpen = true;
    }

    function closeLogWellnessModal() {
        logWellnessModalOpen = false;
    }

    function openViewWellnessModal(wellness: WellnessState) {
        selectedWellness = wellness;
        viewWellnessModalOpen = true;
    }

    function closeViewWellnessModal() {
        viewWellnessModalOpen = false;
        selectedWellness = null;
    }

    function resetWellnessForm() {
        wellnessForm = {
            date: new Date().toISOString().split("T")[0],
            energy: 3,
            motivation: 3,
            mood: 3,
            stress: 3
        };
    }

    $: sortedWellnessStates = [...wellnessStates].sort((a, b) => {
        const dateA = new Date(a.date?.toString() ?? "");
        const dateB = new Date(b.date?.toString() ?? "");
        return sortDirection === "desc" 
            ? dateB.getTime() - dateA.getTime()
            : dateA.getTime() - dateB.getTime();
    });

    $: totalEntries = wellnessStates.length;
    $: averageOverall = totalEntries > 0 
        ? Math.round((wellnessStates.reduce((sum, w) => sum + (w.overallScore ?? 0), 0) / totalEntries) * 10) / 10
        : 0;
    $: averageEnergy = totalEntries > 0 
        ? Math.round((wellnessStates.reduce((sum, w) => sum + (w.energyScore ?? 0), 0) / totalEntries) * 10) / 10
        : 0;
    $: averageMood = totalEntries > 0 
        ? Math.round((wellnessStates.reduce((sum, w) => sum + (w.moodScore ?? 0), 0) / totalEntries) * 10) / 10
        : 0;

    $: calculatedOverall = ((wellnessForm.mood + wellnessForm.energy + wellnessForm.motivation + (6 - wellnessForm.stress)) / 4);

    function formatDate(date: Date | string | undefined): string {
        if (!date) return "N/A";
        // Use UTC timezone since dates are stored/parsed as UTC
        if (date instanceof Date) {
            return date.toLocaleDateString("en-US", {
                month: "short",
                day: "numeric",
                year: "numeric",
                timeZone: "UTC"
            });
        }
        // If string, parse as local date
        const dateStr = date.toString();
        if (dateStr.includes('-') && dateStr.length === 10) {
            const d = parseLocalDate(dateStr);
            return d.toLocaleDateString("en-US", {
                month: "short",
                day: "numeric",
                year: "numeric",
                timeZone: "UTC"
            });
        }
        // Fallback
        const d = new Date(dateStr);
        return d.toLocaleDateString("en-US", {
            month: "short",
            day: "numeric",
            year: "numeric",
            timeZone: "UTC"
        });
    }

    function getScoreColor(score: number | undefined): string {
        if (!score) return "text-base-content/30";
        if (score >= 4) return "text-success";
        if (score >= 3) return "text-warning";
        return "text-error";
    }

    function getProgressColor(score: number | undefined): string {
        if (!score) return "bg-base-300";
        if (score >= 4) return "bg-success";
        if (score >= 3) return "bg-warning";
        return "bg-error";
    }

    function getScoreLabel(score: number | undefined): string {
        if (!score) return "—";
        if (score >= 4.5) return "Excellent";
        if (score >= 4) return "Good";
        if (score >= 3) return "Moderate";
        if (score >= 2) return "Low";
        return "Very Low";
    }

    function getStressColor(score: number | undefined): string {
        if (!score) return "text-base-content/30";
        if (score <= 2) return "text-success";
        if (score <= 3) return "text-warning";
        return "text-error";
    }

    function toggleSort() {
        sortDirection = sortDirection === "asc" ? "desc" : "asc";
    }
</script>

<svelte:head>
    <title>Wellness - Lionheart</title>
</svelte:head>

<div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
    <!-- Header -->
    <div class="mb-8">
        <h1 class="text-4xl sm:text-5xl font-display font-black tracking-tight mb-2">
            Wellness
        </h1>
        <p class="text-base-content/60 text-lg">
            Track your daily energy, motivation, mood, and stress
        </p>
    </div>

    <!-- Today's Status -->
    {#if todaysWellness}
        <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-6 mb-6">
            <div class="flex flex-col lg:flex-row items-start lg:items-center justify-between gap-6">
                <div>
                    <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Today</span>
                    <div class="flex items-baseline gap-2 mt-1">
                        <span class="text-5xl font-display font-black {getScoreColor(todaysWellness.overallScore)}">
                            {todaysWellness.overallScore?.toFixed(1)}
                        </span>
                        <span class="text-xl text-base-content/30 font-bold">/5</span>
                    </div>
                </div>
                <div class="grid grid-cols-4 gap-6 flex-1 max-w-lg">
                    <div>
                        <span class="text-xs font-bold uppercase tracking-wider text-base-content/50 block mb-1">Energy</span>
                        <span class="text-2xl font-display font-black {getScoreColor(todaysWellness.energyScore)}">{todaysWellness.energyScore}</span>
                    </div>
                    <div>
                        <span class="text-xs font-bold uppercase tracking-wider text-base-content/50 block mb-1">Motivation</span>
                        <span class="text-2xl font-display font-black {getScoreColor(todaysWellness.motivationScore)}">{todaysWellness.motivationScore}</span>
                    </div>
                    <div>
                        <span class="text-xs font-bold uppercase tracking-wider text-base-content/50 block mb-1">Mood</span>
                        <span class="text-2xl font-display font-black {getScoreColor(todaysWellness.moodScore)}">{todaysWellness.moodScore}</span>
                    </div>
                    <div>
                        <span class="text-xs font-bold uppercase tracking-wider text-base-content/50 block mb-1">Stress</span>
                        <span class="text-2xl font-display font-black {getStressColor(todaysWellness.stressScore)}">{todaysWellness.stressScore}</span>
                    </div>
                </div>
                <button
                    on:click={() => openLogWellnessModal()}
                    class="btn btn-outline px-5 rounded-xl"
                >
                    Update
                </button>
            </div>
        </div>
    {:else}
        <div class="card bg-base-100 shadow-editorial border-2 border-dashed border-base-content/20 p-6 mb-6">
            <div class="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
                <div>
                    <h3 class="text-xl font-bold">No entry for today</h3>
                    <p class="text-base-content/50 text-sm">Log how you're feeling to track your wellness</p>
                </div>
                <button
                    on:click={() => openLogWellnessModal()}
                    class="btn btn-primary px-6 rounded-xl"
                >
                    Log Today
                </button>
            </div>
        </div>
    {/if}

    <!-- Stats -->
    <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
        <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-4">
            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Entries</span>
            <p class="text-3xl font-display font-black mt-1">{totalEntries}</p>
        </div>
        <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-4">
            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Avg Score</span>
            <p class="text-3xl font-display font-black mt-1 {getScoreColor(averageOverall)}">{averageOverall}</p>
        </div>
        <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-4">
            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Avg Energy</span>
            <p class="text-3xl font-display font-black mt-1 {getScoreColor(averageEnergy)}">{averageEnergy}</p>
        </div>
        <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-4">
            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Avg Mood</span>
            <p class="text-3xl font-display font-black mt-1 {getScoreColor(averageMood)}">{averageMood}</p>
        </div>
    </div>

    <!-- Filters -->
    <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-6 mb-6">
        <div class="flex flex-col lg:flex-row gap-4 items-start lg:items-center justify-between">
            <div class="flex items-center gap-2">
                <input
                    type="date"
                    bind:value={startDate}
                    on:change={loadWellnessStates}
                    class="input input-bordered input-sm rounded-xl font-mono text-xs"
                />
                <span class="text-base-content/50 text-sm font-bold">to</span>
                <input
                    type="date"
                    bind:value={endDate}
                    on:change={loadWellnessStates}
                    class="input input-bordered input-sm rounded-xl font-mono text-xs"
                />
            </div>

            <div class="flex gap-2">
                <button
                    on:click={toggleSort}
                    class="btn btn-outline btn-sm rounded-xl gap-2"
                >
                    {#if sortDirection === "desc"}
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-4 h-4">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 13.5 12 21m0 0-7.5-7.5M12 21V3" />
                        </svg>
                        Newest
                    {:else}
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-4 h-4">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 10.5 12 3m0 0 7.5 7.5M12 3v18" />
                        </svg>
                        Oldest
                    {/if}
                </button>
                
                <button
                    on:click={() => openLogWellnessModal()}
                    class="btn btn-primary btn-sm px-4 rounded-xl gap-2"
                >
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-4 h-4">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
                    </svg>
                    Log
                </button>
            </div>
        </div>

        {#if error}
            <div class="alert alert-error mt-4 rounded-xl">
                <span>{error}</span>
                <button class="btn btn-sm btn-ghost" on:click={() => error = ""}>✕</button>
            </div>
        {/if}
    </div>

    <!-- Grid -->
    {#if loading}
        <div class="flex justify-center items-center py-20">
            <span class="loading loading-spinner loading-lg"></span>
        </div>
    {:else if sortedWellnessStates.length === 0}
        <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-12 text-center">
            <h3 class="text-xl font-bold mb-2">No Data</h3>
            <p class="text-base-content/60 mb-4">
                Start tracking by logging your first wellness entry
            </p>
            <button
                on:click={() => openLogWellnessModal()}
                class="btn btn-primary px-6 rounded-xl mx-auto"
            >
                Log First Entry
            </button>
        </div>
    {:else}
        <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {#each sortedWellnessStates as wellness}
                <button
                    on:click={() => openViewWellnessModal(wellness)}
                    class="card bg-base-100 shadow-editorial border-2 border-base-content/10 
                           hover:border-base-content/30 hover:shadow-editorial-lg 
                           transition-all duration-200 p-5 text-left w-full"
                >
                    <div class="flex items-start justify-between mb-4">
                        <div>
                            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">
                                {formatDate(wellness.date)}
                            </span>
                            <div class="flex items-baseline gap-1 mt-1">
                                <span class="text-3xl font-display font-black {getScoreColor(wellness.overallScore)}">
                                    {wellness.overallScore?.toFixed(1)}
                                </span>
                                <span class="text-base-content/30 text-sm font-bold">/5</span>
                            </div>
                        </div>
                        <span class="badge badge-sm {wellness.overallScore && wellness.overallScore >= 4 ? 'badge-success' : wellness.overallScore && wellness.overallScore >= 3 ? 'badge-warning' : 'badge-error'}">
                            {getScoreLabel(wellness.overallScore)}
                        </span>
                    </div>

                    <div class="space-y-2">
                        <div class="flex items-center gap-3">
                            <span class="text-xs font-bold uppercase tracking-wider text-base-content/50 w-20">Energy</span>
                            <div class="flex-1 h-1.5 bg-base-200 rounded-full overflow-hidden">
                                <div class="h-full {getProgressColor(wellness.energyScore)}" style="width: {((wellness.energyScore ?? 0) / 5) * 100}%"></div>
                            </div>
                            <span class="text-xs font-bold {getScoreColor(wellness.energyScore)} w-4 text-right">{wellness.energyScore}</span>
                        </div>
                        <div class="flex items-center gap-3">
                            <span class="text-xs font-bold uppercase tracking-wider text-base-content/50 w-20">Motivation</span>
                            <div class="flex-1 h-1.5 bg-base-200 rounded-full overflow-hidden">
                                <div class="h-full {getProgressColor(wellness.motivationScore)}" style="width: {((wellness.motivationScore ?? 0) / 5) * 100}%"></div>
                            </div>
                            <span class="text-xs font-bold {getScoreColor(wellness.motivationScore)} w-4 text-right">{wellness.motivationScore}</span>
                        </div>
                        <div class="flex items-center gap-3">
                            <span class="text-xs font-bold uppercase tracking-wider text-base-content/50 w-20">Mood</span>
                            <div class="flex-1 h-1.5 bg-base-200 rounded-full overflow-hidden">
                                <div class="h-full {getProgressColor(wellness.moodScore)}" style="width: {((wellness.moodScore ?? 0) / 5) * 100}%"></div>
                            </div>
                            <span class="text-xs font-bold {getScoreColor(wellness.moodScore)} w-4 text-right">{wellness.moodScore}</span>
                        </div>
                        <div class="flex items-center gap-3">
                            <span class="text-xs font-bold uppercase tracking-wider text-base-content/50 w-20">Stress</span>
                            <div class="flex-1 h-1.5 bg-base-200 rounded-full overflow-hidden">
                                <div class="h-full {wellness.stressScore && wellness.stressScore <= 2 ? 'bg-success' : wellness.stressScore && wellness.stressScore <= 3 ? 'bg-warning' : 'bg-error'}" style="width: {((wellness.stressScore ?? 0) / 5) * 100}%"></div>
                            </div>
                            <span class="text-xs font-bold {getStressColor(wellness.stressScore)} w-4 text-right">{wellness.stressScore}</span>
                        </div>
                    </div>
                </button>
            {/each}
        </div>
    {/if}
</div>

<!-- Log Modal -->
{#if logWellnessModalOpen}
    <div class="modal modal-open">
        <div class="modal-box w-11/12 max-w-lg bg-base-100 p-0 overflow-hidden border-2 border-base-content/20">
            <div class="p-6 pb-4 border-b-2 border-base-content/10">
                <div class="flex items-center justify-between">
                    <div>
                        <h3 class="text-2xl font-display font-black tracking-tight">Log Wellness</h3>
                        <p class="text-xs font-mono uppercase tracking-widest text-base-content/50 mt-1">Record your state</p>
                    </div>
                    <button on:click={closeLogWellnessModal} class="btn btn-ghost btn-sm btn-circle">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>
            </div>

            <form on:submit|preventDefault={logWellness} class="p-6">
                <div class="form-control w-full mb-6">
                    <label class="label" for="wellness-date">
                        <span class="label-text font-bold uppercase text-xs tracking-wider">Date</span>
                    </label>
                    <input
                        id="wellness-date"
                        type="date"
                        class="input input-bordered w-full rounded-xl"
                        bind:value={wellnessForm.date}
                        max={new Date().toISOString().split("T")[0]}
                        required
                    />
                </div>

                <div class="space-y-6">
                    <div class="form-control w-full">
                        <div class="flex items-center justify-between mb-2">
                            <label class="label p-0" for="energy-slider">
                                <span class="label-text font-bold uppercase text-xs tracking-wider">Energy</span>
                            </label>
                            <span class="text-lg font-display font-black {getScoreColor(wellnessForm.energy)}">{wellnessForm.energy}</span>
                        </div>
                        <input id="energy-slider" type="range" min="1" max="5" class="range range-sm" bind:value={wellnessForm.energy} />
                        <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/40">
                            <span>1</span><span>2</span><span>3</span><span>4</span><span>5</span>
                        </div>
                    </div>

                    <div class="form-control w-full">
                        <div class="flex items-center justify-between mb-2">
                            <label class="label p-0" for="motivation-slider">
                                <span class="label-text font-bold uppercase text-xs tracking-wider">Motivation</span>
                            </label>
                            <span class="text-lg font-display font-black {getScoreColor(wellnessForm.motivation)}">{wellnessForm.motivation}</span>
                        </div>
                        <input id="motivation-slider" type="range" min="1" max="5" class="range range-sm" bind:value={wellnessForm.motivation} />
                        <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/40">
                            <span>1</span><span>2</span><span>3</span><span>4</span><span>5</span>
                        </div>
                    </div>

                    <div class="form-control w-full">
                        <div class="flex items-center justify-between mb-2">
                            <label class="label p-0" for="mood-slider">
                                <span class="label-text font-bold uppercase text-xs tracking-wider">Mood</span>
                            </label>
                            <span class="text-lg font-display font-black {getScoreColor(wellnessForm.mood)}">{wellnessForm.mood}</span>
                        </div>
                        <input id="mood-slider" type="range" min="1" max="5" class="range range-sm" bind:value={wellnessForm.mood} />
                        <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/40">
                            <span>1</span><span>2</span><span>3</span><span>4</span><span>5</span>
                        </div>
                    </div>

                    <div class="form-control w-full">
                        <div class="flex items-center justify-between mb-2">
                            <label class="label p-0" for="stress-slider">
                                <span class="label-text font-bold uppercase text-xs tracking-wider">Stress</span>
                            </label>
                            <span class="text-lg font-display font-black {getStressColor(wellnessForm.stress)}">{wellnessForm.stress}</span>
                        </div>
                        <input id="stress-slider" type="range" min="1" max="5" class="range range-sm" bind:value={wellnessForm.stress} />
                        <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/40">
                            <span>1</span><span>2</span><span>3</span><span>4</span><span>5</span>
                        </div>
                    </div>
                </div>

                <div class="bg-base-200 rounded-xl p-4 mt-6 border-2 border-base-content/10">
                    <div class="flex items-center justify-between">
                        <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Overall</span>
                        <div class="flex items-baseline gap-1">
                            <span class="text-2xl font-display font-black {getScoreColor(calculatedOverall)}">{calculatedOverall.toFixed(1)}</span>
                            <span class="text-base-content/30 font-bold">/5</span>
                        </div>
                    </div>
                </div>

                <div class="flex justify-end gap-2 mt-6">
                    <button type="button" class="btn btn-ghost px-5 rounded-xl" on:click={closeLogWellnessModal}>Cancel</button>
                    <button type="submit" class="btn btn-primary px-5 rounded-xl">Save</button>
                </div>
            </form>
        </div>
        <div class="modal-backdrop bg-base-300/80" on:click={closeLogWellnessModal} on:keydown={(e) => e.key === 'Escape' && closeLogWellnessModal()} role="button" tabindex="0" aria-label="Close modal"></div>
    </div>
{/if}

<!-- View Modal -->
{#if viewWellnessModalOpen && selectedWellness}
    <div class="modal modal-open">
        <div class="modal-box w-11/12 max-w-lg bg-base-100 p-0 overflow-hidden border-2 border-base-content/20">
            <div class="p-6 pb-4 border-b-2 border-base-content/10">
                <div class="flex items-center justify-between">
                    <div>
                        <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">{formatDate(selectedWellness.date)}</span>
                        <h3 class="text-2xl font-display font-black tracking-tight mt-1">Wellness</h3>
                    </div>
                    <button on:click={closeViewWellnessModal} class="btn btn-ghost btn-sm btn-circle">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>
            </div>

            <div class="p-6">
                <div class="text-center mb-6 p-6 bg-base-200 rounded-xl border-2 border-base-content/10">
                    <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Overall Score</span>
                    <div class="flex items-baseline justify-center gap-2 mt-2">
                        <span class="text-5xl font-display font-black {getScoreColor(selectedWellness.overallScore)}">
                            {selectedWellness.overallScore?.toFixed(1)}
                        </span>
                        <span class="text-xl text-base-content/30 font-bold">/5</span>
                    </div>
                </div>

                <div class="grid grid-cols-2 gap-4">
                    <div class="p-4 bg-base-200 border-2 border-base-content/10 rounded-xl">
                        <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Energy</span>
                        <div class="flex items-baseline gap-1 mt-2">
                            <span class="text-3xl font-display font-black {getScoreColor(selectedWellness.energyScore)}">{selectedWellness.energyScore}</span>
                            <span class="text-base-content/30 font-bold">/5</span>
                        </div>
                        <div class="h-1.5 bg-base-300 rounded-full overflow-hidden mt-3">
                            <div class="h-full {getProgressColor(selectedWellness.energyScore)}" style="width: {((selectedWellness.energyScore ?? 0) / 5) * 100}%"></div>
                        </div>
                    </div>

                    <div class="p-4 bg-base-200 border-2 border-base-content/10 rounded-xl">
                        <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Motivation</span>
                        <div class="flex items-baseline gap-1 mt-2">
                            <span class="text-3xl font-display font-black {getScoreColor(selectedWellness.motivationScore)}">{selectedWellness.motivationScore}</span>
                            <span class="text-base-content/30 font-bold">/5</span>
                        </div>
                        <div class="h-1.5 bg-base-300 rounded-full overflow-hidden mt-3">
                            <div class="h-full {getProgressColor(selectedWellness.motivationScore)}" style="width: {((selectedWellness.motivationScore ?? 0) / 5) * 100}%"></div>
                        </div>
                    </div>

                    <div class="p-4 bg-base-200 border-2 border-base-content/10 rounded-xl">
                        <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Mood</span>
                        <div class="flex items-baseline gap-1 mt-2">
                            <span class="text-3xl font-display font-black {getScoreColor(selectedWellness.moodScore)}">{selectedWellness.moodScore}</span>
                            <span class="text-base-content/30 font-bold">/5</span>
                        </div>
                        <div class="h-1.5 bg-base-300 rounded-full overflow-hidden mt-3">
                            <div class="h-full {getProgressColor(selectedWellness.moodScore)}" style="width: {((selectedWellness.moodScore ?? 0) / 5) * 100}%"></div>
                        </div>
                    </div>

                    <div class="p-4 bg-base-200 border-2 border-base-content/10 rounded-xl">
                        <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Stress</span>
                        <div class="flex items-baseline gap-1 mt-2">
                            <span class="text-3xl font-display font-black {getStressColor(selectedWellness.stressScore)}">{selectedWellness.stressScore}</span>
                            <span class="text-base-content/30 font-bold">/5</span>
                        </div>
                        <div class="h-1.5 bg-base-300 rounded-full overflow-hidden mt-3">
                            <div class="h-full {selectedWellness.stressScore && selectedWellness.stressScore <= 2 ? 'bg-success' : selectedWellness.stressScore && selectedWellness.stressScore <= 3 ? 'bg-warning' : 'bg-error'}" style="width: {((selectedWellness.stressScore ?? 0) / 5) * 100}%"></div>
                        </div>
                    </div>
                </div>

                <div class="flex justify-end gap-2 mt-6 pt-4 border-t-2 border-base-content/10">
                    <button
                        on:click={() => {
                            closeViewWellnessModal();
                            if (selectedWellness?.date) {
                                openLogWellnessModal(new Date(selectedWellness.date).toISOString().split("T")[0]);
                            }
                        }}
                        class="btn btn-ghost px-5 rounded-xl"
                    >
                        Update
                    </button>
                    <button class="btn btn-primary px-5 rounded-xl" on:click={closeViewWellnessModal}>Close</button>
                </div>
            </div>
        </div>
        <div class="modal-backdrop bg-base-300/80" on:click={closeViewWellnessModal} on:keydown={(e) => e.key === 'Escape' && closeViewWellnessModal()} role="button" tabindex="0" aria-label="Close modal"></div>
    </div>
{/if}
