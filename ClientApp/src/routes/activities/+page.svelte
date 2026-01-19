<script lang="ts">
    import { onMount } from "svelte";
    import { goto } from "$app/navigation";
    import { bootUserDto, fetchBootUserDto, pageUpdate } from "$lib/stores/stores";
    import {
        GetActivitiesEndpointClient,
        AddActivityEndpointClient,
        UpdateActivityEndpointClient,
        DeleteActivityEndpointClient,
        type ActivityDTO,
        CreateActivityRequest,
        UpdateActivityRequest,
        PerceivedEffortRatings
    } from "$lib/api/ApiClient";

    const baseUrl = "";
    
    // API Clients
    const getActivitiesClient = new GetActivitiesEndpointClient(baseUrl);
    const addActivityClient = new AddActivityEndpointClient(baseUrl);
    const updateActivityClient = new UpdateActivityEndpointClient(baseUrl);
    const deleteActivityClient = new DeleteActivityEndpointClient(baseUrl);

    // State
    let activities: ActivityDTO[] = [];
    let loading = false;
    let error = "";
    
    // Date range for filtering
    let startDate = new Date(new Date().setDate(new Date().getDate() - 30)).toISOString().split("T")[0];
    let endDate = new Date().toISOString().split("T")[0];
    
    // Modal states
    let createActivityModalOpen = false;
    let editActivityModalOpen = false;
    let viewActivityModalOpen = false;
    let selectedActivity: ActivityDTO | null = null;
    
    // Form states
    let newActivity = {
        name: "",
        dateTime: new Date().toISOString().slice(0, 16),
        timeInMinutes: 30,
        caloriesBurned: 0,
        userSummary: "",
        includeEffortRatings: false,
        perceivedEffortRatings: {
            difficultyRating: 3,
            engagementRating: 3,
            externalVariablesRating: 3,
            accumulatedFatigue: 3
        }
    };
    
    let editForm = {
        name: "",
        dateTime: "",
        timeInMinutes: 0,
        caloriesBurned: 0,
        userSummary: "",
        includeEffortRatings: false,
        perceivedEffortRatings: {
            difficultyRating: 3,
            engagementRating: 3,
            externalVariablesRating: 3,
            accumulatedFatigue: 3
        }
    };

    // Filter and sort states
    let searchQuery = "";
    let sortBy: "date" | "name" | "duration" | "calories" = "date";
    let sortDirection: "asc" | "desc" = "desc";

    onMount(async () => {
        await fetchBootUserDto(fetch);
        if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
            goto("/auth");
        } else {
            await loadActivities();
        }
    });

    async function loadActivities() {
        loading = true;
        error = "";
        try {
            activities = await getActivitiesClient.get(new Date(startDate), new Date(endDate));
        } catch (err) {
            error = "Failed to load activities";
            console.error(err);
        } finally {
            loading = false;
        }
    }

    async function createActivity() {
        try {
            let perceivedEffort: PerceivedEffortRatings | undefined = undefined;
            
            if (newActivity.includeEffortRatings) {
                perceivedEffort = new PerceivedEffortRatings({
                    recordedAt: new Date(),
                    difficultyRating: newActivity.perceivedEffortRatings.difficultyRating,
                    engagementRating: newActivity.perceivedEffortRatings.engagementRating,
                    externalVariablesRating: newActivity.perceivedEffortRatings.externalVariablesRating,
                    accumulatedFatigue: newActivity.perceivedEffortRatings.accumulatedFatigue
                });
            }

            const request = new CreateActivityRequest({
                name: newActivity.name,
                dateTime: new Date(newActivity.dateTime),
                timeInMinutes: newActivity.timeInMinutes,
                caloriesBurned: newActivity.caloriesBurned,
                userSummary: newActivity.userSummary,
                perceivedEffortRatings: perceivedEffort
            });
            
            await addActivityClient.post(request);
            await loadActivities();
            closeCreateActivityModal();
            resetNewActivityForm();
            pageUpdate.set(new Date());
        } catch (err) {
            error = "Failed to create activity";
            console.error(err);
        }
    }

    async function updateActivity() {
        if (!selectedActivity) return;
        
        try {
            let perceivedEffort: PerceivedEffortRatings | undefined = undefined;
            
            if (editForm.includeEffortRatings) {
                perceivedEffort = new PerceivedEffortRatings({
                    recordedAt: new Date(),
                    difficultyRating: editForm.perceivedEffortRatings.difficultyRating,
                    engagementRating: editForm.perceivedEffortRatings.engagementRating,
                    externalVariablesRating: editForm.perceivedEffortRatings.externalVariablesRating,
                    accumulatedFatigue: editForm.perceivedEffortRatings.accumulatedFatigue
                });
            }

            const request = new UpdateActivityRequest({
                activityID: selectedActivity.activityID,
                name: editForm.name,
                dateTime: new Date(editForm.dateTime),
                timeInMinutes: editForm.timeInMinutes,
                caloriesBurned: editForm.caloriesBurned,
                userSummary: editForm.userSummary,
                perceivedEffortRatings: perceivedEffort
            });
            
            await updateActivityClient.put(request);
            await loadActivities();
            closeEditActivityModal();
            pageUpdate.set(new Date());
        } catch (err) {
            error = "Failed to update activity";
            console.error(err);
        }
    }

    async function deleteActivity(activityId: string) {
        if (!confirm("Are you sure you want to delete this activity? This action cannot be undone.")) {
            return;
        }
        
        try {
            await deleteActivityClient.delete(activityId);
            await loadActivities();
            pageUpdate.set(new Date());
        } catch (err) {
            error = "Failed to delete activity";
            console.error(err);
        }
    }

    // Modal controls
    function openCreateActivityModal() {
        createActivityModalOpen = true;
    }

    function closeCreateActivityModal() {
        createActivityModalOpen = false;
    }

    function openEditActivityModal(activity: ActivityDTO) {
        selectedActivity = activity;
        const hasEffortRatings = activity.perceivedEffortRatings != null;
        editForm = {
            name: activity.name ?? "",
            dateTime: activity.dateTime ? new Date(activity.dateTime).toISOString().slice(0, 16) : "",
            timeInMinutes: activity.timeInMinutes ?? 0,
            caloriesBurned: activity.caloriesBurned ?? 0,
            userSummary: activity.userSummary ?? "",
            includeEffortRatings: hasEffortRatings,
            perceivedEffortRatings: {
                difficultyRating: activity.perceivedEffortRatings?.difficultyRating ?? 3,
                engagementRating: activity.perceivedEffortRatings?.engagementRating ?? 3,
                externalVariablesRating: activity.perceivedEffortRatings?.externalVariablesRating ?? 3,
                accumulatedFatigue: activity.perceivedEffortRatings?.accumulatedFatigue ?? 3
            }
        };
        editActivityModalOpen = true;
    }

    function closeEditActivityModal() {
        editActivityModalOpen = false;
        selectedActivity = null;
    }

    function openViewActivityModal(activity: ActivityDTO) {
        selectedActivity = activity;
        viewActivityModalOpen = true;
    }

    function closeViewActivityModal() {
        viewActivityModalOpen = false;
        selectedActivity = null;
    }

    function resetNewActivityForm() {
        newActivity = {
            name: "",
            dateTime: new Date().toISOString().slice(0, 16),
            timeInMinutes: 30,
            caloriesBurned: 0,
            userSummary: "",
            includeEffortRatings: false,
            perceivedEffortRatings: {
                difficultyRating: 3,
                engagementRating: 3,
                externalVariablesRating: 3,
                accumulatedFatigue: 3
            }
        };
    }

    // Computed
    $: filteredActivities = activities
        .filter(activity => {
            if (searchQuery) {
                const query = searchQuery.toLowerCase();
                return (activity.name?.toLowerCase().includes(query) ||
                        activity.userSummary?.toLowerCase().includes(query));
            }
            return true;
        })
        .sort((a, b) => {
            let comparison = 0;
            switch (sortBy) {
                case "date":
                    comparison = new Date(a.dateTime ?? "").getTime() - new Date(b.dateTime ?? "").getTime();
                    break;
                case "name":
                    comparison = (a.name ?? "").localeCompare(b.name ?? "");
                    break;
                case "duration":
                    comparison = (a.timeInMinutes ?? 0) - (b.timeInMinutes ?? 0);
                    break;
                case "calories":
                    comparison = (a.caloriesBurned ?? 0) - (b.caloriesBurned ?? 0);
                    break;
            }
            return sortDirection === "asc" ? comparison : -comparison;
        });

    // Statistics
    $: totalActivities = activities.length;
    $: totalMinutes = activities.reduce((sum, a) => sum + (a.timeInMinutes ?? 0), 0);
    $: totalCalories = activities.reduce((sum, a) => sum + (a.caloriesBurned ?? 0), 0);
    $: averageDuration = totalActivities > 0 ? Math.round(totalMinutes / totalActivities) : 0;

    // Helpers
    function formatDate(date: Date | string | undefined): string {
        if (!date) return "N/A";
        const d = new Date(date.toString());
        return d.toLocaleDateString("en-US", {
            month: "short",
            day: "numeric",
            year: "numeric",
        });
    }

    function formatDateTime(date: Date | string | undefined): string {
        if (!date) return "N/A";
        const d = new Date(date.toString());
        return d.toLocaleString("en-US", {
            month: "short",
            day: "numeric",
            year: "numeric",
            hour: "numeric",
            minute: "2-digit",
            hour12: true
        });
    }

    function formatDuration(minutes: number | undefined): string {
        if (!minutes) return "0m";
        const hours = Math.floor(minutes / 60);
        const mins = minutes % 60;
        if (hours > 0) {
            return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
        }
        return `${mins}m`;
    }

    function getDifficultyLabel(rating: number | undefined): string {
        if (!rating) return "Not Rated";
        if (rating >= 5) return "Extreme";
        if (rating >= 4) return "Hard";
        if (rating >= 3) return "Moderate";
        if (rating >= 2) return "Easy";
        return "Light";
    }

    function getDifficultyColor(rating: number | undefined): string {
        if (!rating) return "badge-ghost";
        if (rating >= 4) return "badge-error";
        if (rating >= 3) return "badge-warning";
        return "badge-success";
    }

    function getRatingColor(rating: number | undefined): string {
        if (!rating) return "text-base-content/30";
        if (rating >= 4) return "text-success";
        if (rating >= 3) return "text-warning";
        return "text-error";
    }

    function toggleSort(column: typeof sortBy) {
        if (sortBy === column) {
            sortDirection = sortDirection === "asc" ? "desc" : "asc";
        } else {
            sortBy = column;
            sortDirection = "desc";
        }
    }
</script>

<svelte:head>
    <title>Activity Tracking - Lionheart</title>
</svelte:head>

<div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
    <!-- Header -->
    <div class="mb-8">
        <h1 class="text-4xl sm:text-5xl font-display font-black tracking-tight mb-2">
            Activity Tracking
        </h1>
        <p class="text-base-content/60 text-lg">
            Track and manage your daily activities, workouts, and physical efforts
        </p>
    </div>

    <!-- Stats Overview -->
    <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
        <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-4">
            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Total Activities</span>
            <p class="text-3xl font-display font-black mt-1">{totalActivities}</p>
        </div>
        <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-4">
            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Total Time</span>
            <p class="text-3xl font-display font-black mt-1">{formatDuration(totalMinutes)}</p>
        </div>
        <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-4">
            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Calories Burned</span>
            <p class="text-3xl font-display font-black mt-1">{totalCalories.toLocaleString()}</p>
        </div>
        <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-4">
            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Avg Duration</span>
            <p class="text-3xl font-display font-black mt-1">{formatDuration(averageDuration)}</p>
        </div>
    </div>

    <!-- Actions and Filters Bar -->
    <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-6 mb-6">
        <div class="flex flex-col lg:flex-row gap-4 items-start lg:items-center justify-between">
            <!-- Date Range & Search -->
            <div class="flex flex-col sm:flex-row gap-4 flex-1 w-full lg:w-auto">
                <!-- Date Range -->
                <div class="flex items-center gap-2">
                    <input
                        type="date"
                        bind:value={startDate}
                        on:change={loadActivities}
                        class="input input-bordered input-sm rounded-xl font-mono text-xs"
                    />
                    <span class="text-base-content/50 text-sm font-bold">to</span>
                    <input
                        type="date"
                        bind:value={endDate}
                        on:change={loadActivities}
                        class="input input-bordered input-sm rounded-xl font-mono text-xs"
                    />
                </div>

                <!-- Search -->
                <div class="flex-1 min-w-0">
                    <div class="relative">
                        <input
                            type="text"
                            placeholder="Search activities..."
                            class="input input-bordered w-full rounded-xl pr-10"
                            bind:value={searchQuery}
                        />
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5 absolute right-3 top-1/2 -translate-y-1/2 text-base-content/50">
                            <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
                        </svg>
                    </div>
                </div>
            </div>

            <!-- Sort & Create -->
            <div class="flex gap-2 w-full lg:w-auto">
                <select class="select select-bordered rounded-xl font-bold text-sm" bind:value={sortBy} on:change={() => sortDirection = "desc"}>
                    <option value="date">Sort by Date</option>
                    <option value="name">Sort by Name</option>
                    <option value="duration">Sort by Duration</option>
                    <option value="calories">Sort by Calories</option>
                </select>
                
                <button
                    on:click={() => sortDirection = sortDirection === "asc" ? "desc" : "asc"}
                    class="btn btn-square btn-outline rounded-xl"
                    title="Toggle sort direction"
                >
                    {#if sortDirection === "asc"}
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 10.5 12 3m0 0 7.5 7.5M12 3v18" />
                        </svg>
                    {:else}
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 13.5 12 21m0 0-7.5-7.5M12 21V3" />
                        </svg>
                    {/if}
                </button>
                
                <button
                    on:click={openCreateActivityModal}
                    class="btn btn-primary px-5 rounded-xl gap-2"
                >
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
                    </svg>
                    <span class="hidden sm:inline">New Activity</span>
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
                <button class="btn btn-sm btn-ghost" on:click={() => error = ""}>✕</button>
            </div>
        {/if}
    </div>

    <!-- Activities Grid -->
    {#if loading}
        <div class="flex justify-center items-center py-20">
            <span class="loading loading-spinner loading-lg"></span>
        </div>
    {:else if filteredActivities.length === 0}
        <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-12 text-center">
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
                    d="M15.362 5.214A8.252 8.252 0 0 1 12 21 8.25 8.25 0 0 1 6.038 7.047 8.287 8.287 0 0 0 9 9.601a8.983 8.983 0 0 1 3.361-6.867 8.21 8.21 0 0 0 3 2.48Z"
                />
                <path
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    d="M12 18a3.75 3.75 0 0 0 .495-7.468 5.99 5.99 0 0 0-1.925 3.547 5.975 5.975 0 0 1-2.133-1.001A3.75 3.75 0 0 0 12 18Z"
                />
            </svg>
            <h3 class="text-xl font-bold mb-2">No Activities Found</h3>
            <p class="text-base-content/60 mb-4">
                {searchQuery 
                    ? "Try adjusting your search query" 
                    : "Start tracking activities by creating your first entry"}
            </p>
            {#if !searchQuery}
                <button
                    on:click={openCreateActivityModal}
                    class="btn btn-primary px-6 rounded-xl mx-auto"
                >
                    Create First Activity
                </button>
            {/if}
        </div>
    {:else}
        <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {#each filteredActivities as activity}
                <button
                    on:click={() => openViewActivityModal(activity)}
                    class="card bg-base-100 shadow-editorial border-2 border-base-content/10 
                           hover:border-base-content/30 hover:shadow-editorial-lg 
                           transition-all duration-200 p-6 text-left w-full"
                >
                    <!-- Header -->
                    <div class="flex items-start justify-between mb-4">
                        <div class="flex-1 min-w-0">
                            <h3 class="text-xl font-bold truncate">{activity.name}</h3>
                            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">
                                {formatDateTime(activity.dateTime)}
                            </span>
                        </div>
                        <div class="flex flex-col items-end gap-2 ml-2">
                            <span class="badge {getDifficultyColor(activity.perceivedEffortRatings?.difficultyRating)} badge-sm font-bold">
                                {getDifficultyLabel(activity.perceivedEffortRatings?.difficultyRating)}
                            </span>
                        </div>
                    </div>

                    <!-- Summary preview -->
                    {#if activity.userSummary}
                        <p class="text-sm text-base-content/70 mb-4 line-clamp-2">
                            {activity.userSummary}
                        </p>
                    {/if}

                    <!-- Stats -->
                    <div class="grid grid-cols-2 gap-4 pt-4 border-t-2 border-base-content/10">
                        <div class="flex flex-col gap-1">
                            <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Duration</span>
                            <span class="text-2xl font-display font-black">{formatDuration(activity.timeInMinutes)}</span>
                        </div>
                        <div class="flex flex-col gap-1">
                            <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Calories</span>
                            <span class="text-2xl font-display font-black">{activity.caloriesBurned ?? 0}</span>
                        </div>
                    </div>

                    <!-- Quick Actions -->
                    <div class="flex gap-2 mt-4 pt-4 border-t-2 border-base-content/10">
                        <button
                            on:click|stopPropagation={() => openEditActivityModal(activity)}
                            class="btn btn-sm btn-outline rounded-xl flex-1"
                        >
                            Edit
                        </button>
                        <button
                            on:click|stopPropagation={() => deleteActivity(activity.activityID ?? "")}
                            class="btn btn-sm btn-ghost btn-square rounded-xl text-error"
                            title="Delete activity"
                        >
                            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-4 h-4">
                                <path stroke-linecap="round" stroke-linejoin="round" d="m14.74 9-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 0 1-2.244 2.077H8.084a2.25 2.25 0 0 1-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 0 0-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 0 1 3.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 0 0-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 0 0-7.5 0" />
                            </svg>
                        </button>
                    </div>
                </button>
            {/each}
        </div>
    {/if}
</div>

<!-- Create Activity Modal -->
{#if createActivityModalOpen}
    <div class="modal modal-open">
        <div class="modal-box w-11/12 max-w-2xl bg-base-100 p-0 overflow-hidden border-2 border-base-content/20">
            <!-- Header -->
            <div class="p-6 pb-4 border-b-2 border-base-content/10">
                <div class="flex items-center justify-between">
                    <div>
                        <h3 class="text-3xl font-display font-black tracking-tight">New Activity</h3>
                        <p class="text-xs font-mono uppercase tracking-widest text-base-content/50 mt-1">Track your effort</p>
                    </div>
                    <button on:click={closeCreateActivityModal} class="btn btn-ghost btn-sm btn-circle">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>
            </div>

            <!-- Content -->
            <form on:submit|preventDefault={createActivity} class="p-6 max-h-[70vh] overflow-y-auto">
                <!-- Basic Info -->
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
                    <div class="form-control w-full md:col-span-2">
                        <label class="label" for="activity-name">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Activity Name</span>
                        </label>
                        <input
                            id="activity-name"
                            type="text"
                            placeholder="e.g., Morning Run, Yoga Session, Yardwork"
                            class="input input-bordered w-full rounded-xl"
                            bind:value={newActivity.name}
                            required
                        />
                    </div>

                    <div class="form-control w-full">
                        <label class="label" for="activity-date">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Date & Time</span>
                        </label>
                        <input
                            id="activity-date"
                            type="datetime-local"
                            class="input input-bordered w-full rounded-xl"
                            bind:value={newActivity.dateTime}
                            required
                        />
                    </div>

                    <div class="form-control w-full">
                        <label class="label" for="activity-duration">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Duration (minutes)</span>
                        </label>
                        <input
                            id="activity-duration"
                            type="number"
                            min="1"
                            placeholder="30"
                            class="input input-bordered w-full rounded-xl"
                            bind:value={newActivity.timeInMinutes}
                            required
                        />
                    </div>

                    <div class="form-control w-full md:col-span-2">
                        <label class="label" for="activity-calories">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Calories Burned </span>
                        </label>
                        <input
                            id="activity-calories"
                            type="number"
                            min="0"
                            placeholder="0"
                            class="input input-bordered w-full rounded-xl"
                            bind:value={newActivity.caloriesBurned}
                        />
                    </div>

                    <div class="form-control w-full md:col-span-2">
                        <label class="label" for="activity-summary">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Summary / Notes</span>
                        </label>
                        <textarea
                            id="activity-summary"
                            placeholder="Describe the activity, how you felt, any notable details..."
                            class="textarea textarea-bordered w-full h-24 rounded-xl"
                            bind:value={newActivity.userSummary}
                        ></textarea>
                    </div>
                </div>

                <!-- Perceived Effort Ratings -->
                <div class="bg-base-200 rounded-xl p-4 mb-6 border-2 border-base-content/10">
                    <div class="flex items-center justify-between mb-4">
                        <h4 class="font-bold uppercase text-sm tracking-wider">Perceived Effort Ratings</h4>
                        <label class="label cursor-pointer gap-3">
                            <span class="label-text text-xs font-bold uppercase tracking-wider text-base-content/60">
                                {newActivity.includeEffortRatings ? 'Enabled' : 'Disabled'}
                            </span>
                            <input
                                type="checkbox"
                                class="toggle toggle-primary toggle-sm"
                                bind:checked={newActivity.includeEffortRatings}
                            />
                        </label>
                    </div>
                    
                    {#if newActivity.includeEffortRatings}
                    <div class="grid grid-cols-1 sm:grid-cols-2 gap-6">
                        <!-- Difficulty -->
                        <div class="form-control w-full">
                            <label class="label" for="new-difficulty">
                                <span class="label-text font-bold uppercase text-xs tracking-wider">Difficulty</span>
                                <span class="label-text-alt text-lg font-display font-black {getRatingColor(newActivity.perceivedEffortRatings.difficultyRating)}">{newActivity.perceivedEffortRatings.difficultyRating}/5</span>
                            </label>
                            <input
                                id="new-difficulty"
                                type="range"
                                min="1"
                                max="5"
                                class="range range-primary range-sm"
                                bind:value={newActivity.perceivedEffortRatings.difficultyRating}
                            />
                            <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/50">
                                <span>Easy</span>
                                <span>Hard</span>
                            </div>
                        </div>

                        <!-- Engagement -->
                        <div class="form-control w-full">
                            <label class="label" for="new-engagement">
                                <span class="label-text font-bold uppercase text-xs tracking-wider">Engagement</span>
                                <span class="label-text-alt text-lg font-display font-black {getRatingColor(newActivity.perceivedEffortRatings.engagementRating)}">{newActivity.perceivedEffortRatings.engagementRating}/5</span>
                            </label>
                            <input
                                id="new-engagement"
                                type="range"
                                min="1"
                                max="5"
                                class="range range-info range-sm"
                                bind:value={newActivity.perceivedEffortRatings.engagementRating}
                            />
                            <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/50">
                                <span>Low</span>
                                <span>High</span>
                            </div>
                        </div>

                        <!-- External Variables -->
                        <div class="form-control w-full">
                            <label class="label" for="new-external">
                                <span class="label-text font-bold uppercase text-xs tracking-wider">External Variables</span>
                                <span class="label-text-alt text-lg font-display font-black {getRatingColor(newActivity.perceivedEffortRatings.externalVariablesRating)}">{newActivity.perceivedEffortRatings.externalVariablesRating}/5</span>
                            </label>
                            <input
                                id="new-external"
                                type="range"
                                min="1"
                                max="5"
                                class="range range-warning range-sm"
                                bind:value={newActivity.perceivedEffortRatings.externalVariablesRating}
                            />
                            <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/50">
                                <span>Minimal</span>
                                <span>Significant</span>
                            </div>
                        </div>

                        <!-- Accumulated Fatigue -->
                        <div class="form-control w-full">
                            <label class="label" for="new-fatigue">
                                <span class="label-text font-bold uppercase text-xs tracking-wider">Accumulated Fatigue</span>
                                <span class="label-text-alt text-lg font-display font-black {getRatingColor(newActivity.perceivedEffortRatings.accumulatedFatigue)}">{newActivity.perceivedEffortRatings.accumulatedFatigue}/5</span>
                            </label>
                            <input
                                id="new-fatigue"
                                type="range"
                                min="1"
                                max="5"
                                class="range range-error range-sm"
                                bind:value={newActivity.perceivedEffortRatings.accumulatedFatigue}
                            />
                            <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/50">
                                <span>Fresh</span>
                                <span>Exhausted</span>
                            </div>
                        </div>
                    </div>
                    {:else}
                    <p class="text-sm text-base-content/50 italic">Toggle on to track how the activity felt</p>
                    {/if}
                </div>

                <div class="flex justify-end gap-2">
                    <button type="button" class="btn btn-outline px-5 rounded-xl" on:click={closeCreateActivityModal}>Cancel</button>
                    <button type="submit" class="btn btn-primary px-5 rounded-xl">Create Activity</button>
                </div>
            </form>
        </div>
        <div class="modal-backdrop bg-base-300/80" on:click={closeCreateActivityModal} on:keydown={(e) => e.key === 'Escape' && closeCreateActivityModal()} role="button" tabindex="0" aria-label="Close modal"></div>
    </div>
{/if}

<!-- Edit Activity Modal -->
{#if editActivityModalOpen && selectedActivity}
    <div class="modal modal-open">
        <div class="modal-box w-11/12 max-w-2xl bg-base-100 p-0 overflow-hidden border-2 border-base-content/20">
            <!-- Header -->
            <div class="p-6 pb-4 border-b-2 border-base-content/10">
                <div class="flex items-center justify-between">
                    <div>
                        <h3 class="text-3xl font-display font-black tracking-tight">Edit Activity</h3>
                        <p class="text-xs font-mono uppercase tracking-widest text-base-content/50 mt-1">Update details</p>
                    </div>
                    <button on:click={closeEditActivityModal} class="btn btn-ghost btn-sm btn-circle">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>
            </div>

            <!-- Content -->
            <form on:submit|preventDefault={updateActivity} class="p-6 max-h-[70vh] overflow-y-auto">
                <!-- Basic Info -->
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
                    <div class="form-control w-full md:col-span-2">
                        <label class="label" for="edit-activity-name">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Activity Name</span>
                        </label>
                        <input
                            id="edit-activity-name"
                            type="text"
                            class="input input-bordered w-full rounded-xl"
                            bind:value={editForm.name}
                            required
                        />
                    </div>

                    <div class="form-control w-full">
                        <label class="label" for="edit-activity-date">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Date & Time</span>
                        </label>
                        <input
                            id="edit-activity-date"
                            type="datetime-local"
                            class="input input-bordered w-full rounded-xl"
                            bind:value={editForm.dateTime}
                            required
                        />
                    </div>

                    <div class="form-control w-full">
                        <label class="label" for="edit-activity-duration">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Duration (minutes)</span>
                        </label>
                        <input
                            id="edit-activity-duration"
                            type="number"
                            min="1"
                            class="input input-bordered w-full rounded-xl"
                            bind:value={editForm.timeInMinutes}
                            required
                        />
                    </div>

                    <div class="form-control w-full md:col-span-2">
                        <label class="label" for="edit-activity-calories">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Calories Burned</span>
                        </label>
                        <input
                            id="edit-activity-calories"
                            type="number"
                            min="0"
                            class="input input-bordered w-full rounded-xl"
                            bind:value={editForm.caloriesBurned}
                        />
                    </div>

                    <div class="form-control w-full md:col-span-2">
                        <label class="label" for="edit-activity-summary">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Summary / Notes</span>
                        </label>
                        <textarea
                            id="edit-activity-summary"
                            class="textarea textarea-bordered w-full h-24 rounded-xl"
                            bind:value={editForm.userSummary}
                        ></textarea>
                    </div>
                </div>

                <!-- Perceived Effort Ratings -->
                <div class="bg-base-200 rounded-xl p-4 mb-6 border-2 border-base-content/10">
                    <div class="flex items-center justify-between mb-4">
                        <h4 class="font-bold uppercase text-sm tracking-wider">Perceived Effort Ratings</h4>
                        <label class="label cursor-pointer gap-3">
                            <span class="label-text text-xs font-bold uppercase tracking-wider text-base-content/60">
                                {editForm.includeEffortRatings ? 'Enabled' : 'Disabled'}
                            </span>
                            <input
                                type="checkbox"
                                class="toggle toggle-primary toggle-sm"
                                bind:checked={editForm.includeEffortRatings}
                            />
                        </label>
                    </div>
                    
                    {#if editForm.includeEffortRatings}
                    <div class="grid grid-cols-1 sm:grid-cols-2 gap-6">
                        <!-- Difficulty -->
                        <div class="form-control w-full">
                            <label class="label" for="edit-difficulty">
                                <span class="label-text font-bold uppercase text-xs tracking-wider">Difficulty</span>
                                <span class="label-text-alt text-lg font-display font-black {getRatingColor(editForm.perceivedEffortRatings.difficultyRating)}">{editForm.perceivedEffortRatings.difficultyRating}/5</span>
                            </label>
                            <input
                                id="edit-difficulty"
                                type="range"
                                min="1"
                                max="5"
                                class="range range-primary range-sm"
                                bind:value={editForm.perceivedEffortRatings.difficultyRating}
                            />
                            <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/50">
                                <span>Easy</span>
                                <span>Hard</span>
                            </div>
                        </div>

                        <!-- Engagement -->
                        <div class="form-control w-full">
                            <label class="label" for="edit-engagement">
                                <span class="label-text font-bold uppercase text-xs tracking-wider">Engagement</span>
                                <span class="label-text-alt text-lg font-display font-black {getRatingColor(editForm.perceivedEffortRatings.engagementRating)}">{editForm.perceivedEffortRatings.engagementRating}/5</span>
                            </label>
                            <input
                                id="edit-engagement"
                                type="range"
                                min="1"
                                max="5"
                                class="range range-info range-sm"
                                bind:value={editForm.perceivedEffortRatings.engagementRating}
                            />
                            <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/50">
                                <span>Low</span>
                                <span>High</span>
                            </div>
                        </div>

                        <!-- External Variables -->
                        <div class="form-control w-full">
                            <label class="label" for="edit-external">
                                <span class="label-text font-bold uppercase text-xs tracking-wider">External Variables</span>
                                <span class="label-text-alt text-lg font-display font-black {getRatingColor(editForm.perceivedEffortRatings.externalVariablesRating)}">{editForm.perceivedEffortRatings.externalVariablesRating}/5</span>
                            </label>
                            <input
                                id="edit-external"
                                type="range"
                                min="1"
                                max="5"
                                class="range range-warning range-sm"
                                bind:value={editForm.perceivedEffortRatings.externalVariablesRating}
                            />
                            <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/50">
                                <span>Minimal</span>
                                <span>Significant</span>
                            </div>
                        </div>

                        <!-- Accumulated Fatigue -->
                        <div class="form-control w-full">
                            <label class="label" for="edit-fatigue">
                                <span class="label-text font-bold uppercase text-xs tracking-wider">Accumulated Fatigue</span>
                                <span class="label-text-alt text-lg font-display font-black {getRatingColor(editForm.perceivedEffortRatings.accumulatedFatigue)}">{editForm.perceivedEffortRatings.accumulatedFatigue}/5</span>
                            </label>
                            <input
                                id="edit-fatigue"
                                type="range"
                                min="1"
                                max="5"
                                class="range range-error range-sm"
                                bind:value={editForm.perceivedEffortRatings.accumulatedFatigue}
                            />
                            <div class="w-full flex justify-between text-xs px-1 mt-1 text-base-content/50">
                                <span>Fresh</span>
                                <span>Exhausted</span>
                            </div>
                        </div>
                    </div>
                    {:else}
                    <p class="text-sm text-base-content/50 italic">Toggle on to track how the activity felt</p>
                    {/if}
                </div>

                <div class="flex justify-end gap-2">
                    <button type="button" class="btn btn-outline px-5 rounded-xl" on:click={closeEditActivityModal}>Cancel</button>
                    <button type="submit" class="btn btn-primary px-5 rounded-xl">Save Changes</button>
                </div>
            </form>
        </div>
        <div class="modal-backdrop bg-base-300/80" on:click={closeEditActivityModal} on:keydown={(e) => e.key === 'Escape' && closeEditActivityModal()} role="button" tabindex="0" aria-label="Close modal"></div>
    </div>
{/if}

<!-- View Activity Details Modal -->
{#if viewActivityModalOpen && selectedActivity}
    <div class="modal modal-open">
        <div class="modal-box w-11/12 max-w-2xl bg-base-100 p-0 overflow-hidden border-2 border-base-content/20">
            <!-- Header -->
            <div class="p-6 pb-4 border-b-2 border-base-content/10">
                <div class="flex items-center justify-between">
                    <div>
                        <h3 class="text-3xl font-display font-black tracking-tight">{selectedActivity.name}</h3>
                        <p class="text-xs font-mono uppercase tracking-widest text-base-content/50 mt-1">{formatDateTime(selectedActivity.dateTime)}</p>
                    </div>
                    <button on:click={closeViewActivityModal} class="btn btn-ghost btn-sm btn-circle">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>
            </div>

            <!-- Content -->
            <div class="p-6">
                <!-- Primary Stats -->
                <div class="grid grid-cols-2 gap-4 mb-6">
                    <div class="p-5 bg-base-200 border-2 border-base-content/10 rounded-xl">
                        <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Duration</span>
                        <p class="text-4xl font-display font-black mt-1">{formatDuration(selectedActivity.timeInMinutes)}</p>
                    </div>
                    <div class="p-5 bg-base-200 border-2 border-base-content/10 rounded-xl">
                        <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Calories</span>
                        <p class="text-4xl font-display font-black mt-1">{selectedActivity.caloriesBurned ?? 0}</p>
                    </div>
                </div>

                <!-- Summary -->
                {#if selectedActivity.userSummary}
                    <div class="mb-6">
                        <h4 class="text-xs font-bold uppercase tracking-widest text-base-content/50 mb-2">Summary</h4>
                        <p class="text-base-content/80 bg-base-200 p-4 rounded-xl border-2 border-base-content/10">{selectedActivity.userSummary}</p>
                    </div>
                {/if}

                <!-- Perceived Effort Ratings -->
                {#if selectedActivity.perceivedEffortRatings}
                    <div class="bg-base-200 rounded-xl p-5 border-2 border-base-content/10">
                        <h4 class="text-xs font-bold uppercase tracking-widest text-base-content/50 mb-4">Perceived Effort</h4>
                        
                        <div class="grid grid-cols-2 gap-4">
                            <div class="flex flex-col gap-2">
                                <div class="flex items-center justify-between">
                                    <span class="text-sm font-bold text-base-content/70">Difficulty</span>
                                    <span class="text-xl font-display font-black {getRatingColor(selectedActivity.perceivedEffortRatings.difficultyRating)}">
                                        {selectedActivity.perceivedEffortRatings.difficultyRating ?? "-"}/5
                                    </span>
                                </div>
                                <div class="h-2 bg-base-300 rounded-full overflow-hidden">
                                    <div 
                                        class="h-full bg-primary transition-all duration-300" 
                                        style="width: {((selectedActivity.perceivedEffortRatings.difficultyRating ?? 0) / 5) * 100}%"
                                    ></div>
                                </div>
                            </div>

                            <div class="flex flex-col gap-2">
                                <div class="flex items-center justify-between">
                                    <span class="text-sm font-bold text-base-content/70">Engagement</span>
                                    <span class="text-xl font-display font-black {getRatingColor(selectedActivity.perceivedEffortRatings.engagementRating)}">
                                        {selectedActivity.perceivedEffortRatings.engagementRating ?? "-"}/5
                                    </span>
                                </div>
                                <div class="h-2 bg-base-300 rounded-full overflow-hidden">
                                    <div 
                                        class="h-full bg-info transition-all duration-300" 
                                        style="width: {((selectedActivity.perceivedEffortRatings.engagementRating ?? 0) / 5) * 100}%"
                                    ></div>
                                </div>
                            </div>

                            <div class="flex flex-col gap-2">
                                <div class="flex items-center justify-between">
                                    <span class="text-sm font-bold text-base-content/70">External Vars</span>
                                    <span class="text-xl font-display font-black {getRatingColor(selectedActivity.perceivedEffortRatings.externalVariablesRating)}">
                                        {selectedActivity.perceivedEffortRatings.externalVariablesRating ?? "-"}/5
                                    </span>
                                </div>
                                <div class="h-2 bg-base-300 rounded-full overflow-hidden">
                                    <div 
                                        class="h-full bg-warning transition-all duration-300" 
                                        style="width: {((selectedActivity.perceivedEffortRatings.externalVariablesRating ?? 0) / 5) * 100}%"
                                    ></div>
                                </div>
                            </div>

                            <div class="flex flex-col gap-2">
                                <div class="flex items-center justify-between">
                                    <span class="text-sm font-bold text-base-content/70">Fatigue</span>
                                    <span class="text-xl font-display font-black {getRatingColor(selectedActivity.perceivedEffortRatings.accumulatedFatigue)}">
                                        {selectedActivity.perceivedEffortRatings.accumulatedFatigue ?? "-"}/5
                                    </span>
                                </div>
                                <div class="h-2 bg-base-300 rounded-full overflow-hidden">
                                    <div 
                                        class="h-full bg-error transition-all duration-300" 
                                        style="width: {((selectedActivity.perceivedEffortRatings.accumulatedFatigue ?? 0) / 5) * 100}%"
                                    ></div>
                                </div>
                            </div>
                        </div>
                    </div>
                {/if}

                <!-- Actions -->
                <div class="flex justify-end gap-2 mt-6 pt-4 border-t-2 border-base-content/10">
                    <button
                        on:click={() => {
                            closeViewActivityModal();
                            if (selectedActivity) openEditActivityModal(selectedActivity);
                        }}
                        class="btn btn-outline px-5 rounded-xl"
                    >
                        Edit
                    </button>
                    <button
                        on:click={() => {
                            if (selectedActivity) {
                                deleteActivity(selectedActivity.activityID ?? "");
                                closeViewActivityModal();
                            }
                        }}
                        class="btn btn-error btn-outline px-5 rounded-xl"
                    >
                        Delete
                    </button>
                    <button class="btn btn-primary px-5 rounded-xl" on:click={closeViewActivityModal}>Close</button>
                </div>
            </div>
        </div>
        <div class="modal-backdrop bg-base-300/80" on:click={closeViewActivityModal} on:keydown={(e) => e.key === 'Escape' && closeViewActivityModal()} role="button" tabindex="0" aria-label="Close modal"></div>
    </div>
{/if}
