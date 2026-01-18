<script lang="ts">
    import { onMount } from "svelte";
    import { goto } from "$app/navigation";
    import { bootUserDto, fetchBootUserDto } from "$lib/stores/stores";
    import {
        GetInjuriesEndpointClient,
        CreateInjuryEndpointClient,
        UpdateInjuryEndpointClient,
        DeleteInjuryEndpointClient,
        CreateInjuryEventEndpointClient,
        DeleteInjuryEventEndpointClient,
        type InjuryDTO,
        type InjuryEventDTO,
        CreateInjuryRequest,
        CreateInjuryEventRequest,
        UpdateInjuryRequest,
        InjuryEventType
    } from "$lib/api/ApiClient";

    const baseUrl = "";
    
    // API Clients
    const getInjuriesClient = new GetInjuriesEndpointClient(baseUrl);
    const createInjuryClient = new CreateInjuryEndpointClient(baseUrl);
    const updateInjuryClient = new UpdateInjuryEndpointClient(baseUrl);
    const deleteInjuryClient = new DeleteInjuryEndpointClient(baseUrl);
    const createEventClient = new CreateInjuryEventEndpointClient(baseUrl);
    const deleteEventClient = new DeleteInjuryEventEndpointClient(baseUrl);

    // State
    let injuries: InjuryDTO[] = [];
    let loading = false;
    let error = "";
    
    // Modal states
    let createInjuryModalOpen = false;
    let addEventModalOpen = false;
    let editInjuryModalOpen = false;
    let viewInjuryModalOpen = false;
    let selectedInjury: InjuryDTO | null = null;
    
    // Form states
    let newInjury = {
        name: "",
        injuryDate: new Date().toISOString().split("T")[0],
        notes: ""
    };
    
    let newEvent = {
        painLevel: 5,
        notes: "",
        injuryType: InjuryEventType._1 // checkin
    };
    
    let editForm = {
        name: "",
        notes: "",
        isActive: false
    };

    // Filter states
    let filterActive = "all"; // "all", "active", "inactive"
    let searchQuery = "";

    onMount(async () => {
        await fetchBootUserDto(fetch);
        if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
            goto("/auth");
        } else {
            await loadInjuries();
        }
    });

    async function loadInjuries() {
        loading = true;
        error = "";
        try {
            injuries = await getInjuriesClient.get();
            // Sort by injury date descending (most recent first)
            injuries.sort((a, b) => {
                const dateA = new Date(a.injuryDate.toString());
                const dateB = new Date(b.injuryDate.toString());
                return dateB.getTime() - dateA.getTime();
            });
        } catch (err) {
            error = "Failed to load injuries";
            console.error(err);
        } finally {
            loading = false;
        }
    }

    async function createInjury() {
        try {
            const request = new CreateInjuryRequest({
                name: newInjury.name,
                injuryDate: newInjury.injuryDate as any,
                notes: newInjury.notes
            });
            
            await createInjuryClient.post(request);
            await loadInjuries();
            closeCreateInjuryModal();
            resetNewInjuryForm();
        } catch (err) {
            error = "Failed to create injury";
            console.error(err);
        }
    }

    async function updateInjury() {
        if (!selectedInjury) return;
        
        try {
            const request = new UpdateInjuryRequest({
                injuryID: selectedInjury.injuryID,
                name: editForm.name,
                notes: editForm.notes,
                isActive: editForm.isActive
            });
            
            await updateInjuryClient.put(request);
            await loadInjuries();
            closeEditInjuryModal();
        } catch (err) {
            error = "Failed to update injury";
            console.error(err);
        }
    }

    async function deleteInjury(injuryId: string) {
        if (!confirm("Are you sure you want to delete this injury? This action cannot be undone.")) {
            return;
        }
        
        try {
            await deleteInjuryClient.delete(injuryId);
            await loadInjuries();
        } catch (err) {
            error = "Failed to delete injury";
            console.error(err);
        }
    }

    async function addInjuryEvent() {
        if (!selectedInjury) return;
        
        try {
            const request = new CreateInjuryEventRequest({
                injuryID: selectedInjury.injuryID,
                painLevel: newEvent.painLevel,
                notes: newEvent.notes,
                injuryType: newEvent.injuryType,
                movementIDs: []
            });
            
            await createEventClient.post(request);
            await loadInjuries();
            closeAddEventModal();
            resetEventForm();
        } catch (err) {
            error = "Failed to add injury event";
            console.error(err);
        }
    }

    async function deleteEvent(eventId: string) {
        if (!confirm("Are you sure you want to delete this event?")) {
            return;
        }
        
        try {
            await deleteEventClient.delete(eventId);
            await loadInjuries();
            // Refresh selected injury if viewing
            if (selectedInjury) {
                const updated = injuries.find(i => i.injuryID === selectedInjury!.injuryID);
                if (updated) selectedInjury = updated;
            }
        } catch (err) {
            error = "Failed to delete event";
            console.error(err);
        }
    }

    // Modal controls
    function openCreateInjuryModal() {
        createInjuryModalOpen = true;
    }

    function closeCreateInjuryModal() {
        createInjuryModalOpen = false;
    }

    function openAddEventModal(injury: InjuryDTO) {
        selectedInjury = injury;
        addEventModalOpen = true;
    }

    function closeAddEventModal() {
        addEventModalOpen = false;
        selectedInjury = null;
    }

    function openEditInjuryModal(injury: InjuryDTO) {
        selectedInjury = injury;
        editForm = {
            name: injury.name,
            notes: injury.notes,
            isActive: injury.isActive
        };
        editInjuryModalOpen = true;
    }

    function closeEditInjuryModal() {
        editInjuryModalOpen = false;
        selectedInjury = null;
    }

    function openViewInjuryModal(injury: InjuryDTO) {
        selectedInjury = injury;
        viewInjuryModalOpen = true;
    }

    function closeViewInjuryModal() {
        viewInjuryModalOpen = false;
        selectedInjury = null;
    }

    function resetNewInjuryForm() {
        newInjury = {
            name: "",
            injuryDate: new Date().toISOString().split("T")[0],
            notes: ""
        };
    }

    function resetEventForm() {
        newEvent = {
            painLevel: 5,
            notes: "",
            injuryType: InjuryEventType._1
        };
    }

    // Computed
    $: filteredInjuries = injuries.filter(injury => {
        // Filter by active status
        if (filterActive === "active" && !injury.isActive) return false;
        if (filterActive === "inactive" && injury.isActive) return false;
        
        // Filter by search query
        if (searchQuery) {
            const query = searchQuery.toLowerCase();
            return injury.name.toLowerCase().includes(query) ||
                   injury.notes.toLowerCase().includes(query);
        }
        
        return true;
    });

    function formatDate(date: any): string {
        if (!date) return "N/A";
        const d = new Date(date.toString());
        return d.toLocaleDateString("en-US", {
            month: "short",
            day: "numeric",
            year: "numeric",
        });
    }

    function formatDateTime(date: any): string {
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

    function getPainLevelColor(level: number): string {
        if (level <= 3) return "text-success";
        if (level <= 6) return "text-warning";
        return "text-error";
    }

    function getEventTypeLabel(type: InjuryEventType): string {
        return type === InjuryEventType._0 ? "Treatment" : "Check-in";
    }

    function getEventTypeColor(type: InjuryEventType): string {
        return type === InjuryEventType._0 ? "badge-info" : "badge-secondary";
    }
</script>

<svelte:head>
    <title>Injury Tracking - Lionheart</title>
</svelte:head>

<div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
    <!-- Header -->
    <div class="mb-8">
        <h1 class="text-4xl font-display font-black tracking-tight mb-2">
            Injury Tracking
        </h1>
        <p class="text-base-content/60 text-lg">
            Monitor and manage your injury history and recovery progress
        </p>
    </div>

    <!-- Actions and Filters Bar -->
    <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-6 mb-6">
        <div class="flex flex-col sm:flex-row gap-4 items-start sm:items-center justify-between">
            <!-- Search -->
            <div class="flex-1 w-full sm:w-auto">
                <div class="form-control">
                    <div class="input-group">
                        <input
                            type="text"
                            placeholder="Search injuries..."
                            class="input input-bordered w-full rounded-xl"
                            bind:value={searchQuery}
                        />
                        <button class="btn btn-square rounded-xl">
                            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5">
                                <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
                            </svg>
                        </button>
                    </div>
                </div>
            </div>

            <!-- Filter & Create -->
            <div class="flex gap-2 w-full sm:w-auto">
                <select class="select select-bordered rounded-xl font-bold" bind:value={filterActive}>
                    <option value="all">All Injuries</option>
                    <option value="active">Active Only</option>
                    <option value="inactive">Inactive Only</option>
                </select>
                
                <button
                    on:click={openCreateInjuryModal}
                    class="btn btn-primary rounded-xl font-bold uppercase tracking-wider"
                >
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
                    </svg>
                    New Injury
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

    <!-- Injuries Grid -->
    {#if loading}
        <div class="flex justify-center items-center py-20">
            <span class="loading loading-spinner loading-lg"></span>
        </div>
    {:else if filteredInjuries.length === 0}
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
                    d="M9.879 7.519c1.171-1.025 3.071-1.025 4.242 0 1.172 1.025 1.172 2.687 0 3.712-.203.179-.43.326-.67.442-.745.361-1.45.999-1.45 1.827v.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9 5.25h.008v.008H12v-.008Z"
                />
            </svg>
            <h3 class="text-xl font-bold mb-2">No Injuries Found</h3>
            <p class="text-base-content/60 mb-4">
                {searchQuery || filterActive !== "all" 
                    ? "Try adjusting your filters or search query" 
                    : "Start tracking injuries by creating your first entry"}
            </p>
            {#if !searchQuery && filterActive === "all"}
                <button
                    on:click={openCreateInjuryModal}
                    class="btn btn-primary rounded-xl font-bold uppercase tracking-wider mx-auto"
                >
                    Create First Injury
                </button>
            {/if}
        </div>
    {:else}
        <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {#each filteredInjuries as injury}
                <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 hover:border-base-content/30 transition-all duration-200 p-6">
                    <!-- Header -->
                    <div class="flex items-start justify-between mb-4">
                        <div class="flex-1">
                            <h3 class="text-xl font-bold mb-1">{injury.name}</h3>
                            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">
                                {formatDate(injury.injuryDate)}
                            </span>
                        </div>
                        <div class="flex flex-col items-end gap-2">
                            {#if injury.isActive}
                                <span class="badge badge-error badge-sm">Active</span>
                            {:else}
                                <span class="badge badge-success badge-sm">Recovered</span>
                            {/if}
                        </div>
                    </div>

                    <!-- Notes preview -->
                    {#if injury.notes}
                        <p class="text-sm text-base-content/70 mb-4 line-clamp-2">
                            {injury.notes}
                        </p>
                    {/if}

                    <!-- Events Summary -->
                    <div class="flex items-center gap-4 mb-4 text-sm">
                        <div class="flex items-center gap-1 text-base-content/60">
                            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-4 h-4">
                                <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
                            </svg>
                            <span class="font-bold">{injury.injuryEvents.length}</span>
                            <span>events</span>
                        </div>
                        {#if injury.injuryEvents.length > 0}
                            {@const latestEvent = injury.injuryEvents.reduce((latest, event) => 
                                new Date(event.creationTime) > new Date(latest.creationTime) ? event : latest
                            )}
                            <div class="flex items-center gap-1 text-base-content/60">
                                <span>Last:</span>
                                <span class="font-bold {getPainLevelColor(latestEvent.painLevel)}">
                                    {latestEvent.painLevel}/10
                                </span>
                            </div>
                        {/if}
                    </div>

                    <!-- Actions -->
                    <div class="flex gap-2">
                        <button
                            on:click={() => openViewInjuryModal(injury)}
                            class="btn btn-sm btn-outline rounded-xl flex-1 font-bold uppercase"
                        >
                            View
                        </button>
                        <button
                            on:click={() => openAddEventModal(injury)}
                            class="btn btn-sm btn-primary rounded-xl flex-1 font-bold uppercase"
                        >
                            Add Event
                        </button>
                        <div class="dropdown dropdown-end">
                            <button type="button" tabindex="0" class="btn btn-sm btn-ghost btn-square rounded-xl">
                                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-4 h-4">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M12 6.75a.75.75 0 1 1 0-1.5.75.75 0 0 1 0 1.5ZM12 12.75a.75.75 0 1 1 0-1.5.75.75 0 0 1 0 1.5ZM12 18.75a.75.75 0 1 1 0-1.5.75.75 0 0 1 0 1.5Z" />
                                </svg>
                            </button>
                            <!-- svelte-ignore a11y-no-noninteractive-tabindex -->
                            <ul tabindex="0" class="dropdown-content menu p-2 shadow-lg bg-base-100 rounded-xl w-52 border-2 border-base-content/10">
                                <li><button on:click={() => openEditInjuryModal(injury)}>Edit</button></li>
                                <li><button on:click={() => deleteInjury(injury.injuryID)} class="text-error">Delete</button></li>
                            </ul>
                        </div>
                    </div>
                </div>
            {/each}
        </div>
    {/if}
</div>

<!-- Create Injury Modal -->
{#if createInjuryModalOpen}
    <div class="modal modal-open">
        <div class="modal-box w-11/12 max-w-lg">
            <button class="btn btn-sm btn-circle btn-ghost absolute right-2 top-2" on:click={closeCreateInjuryModal}>✕</button>
            
            <h3 class="font-bold text-2xl mb-2">Create New Injury</h3>
            <p class="text-sm text-base-content/70 mb-6">
                Record a new injury to start tracking your recovery
            </p>

            <form on:submit|preventDefault={createInjury}>
                <div class="form-control w-full mb-4">
                    <label class="label" for="injury-name">
                        <span class="label-text font-bold uppercase text-xs tracking-wider">Injury Name</span>
                    </label>
                    <input
                        id="injury-name"
                        type="text"
                        placeholder="e.g., Left Knee Pain, Right Shoulder Strain"
                        class="input input-bordered w-full rounded-xl"
                        bind:value={newInjury.name}
                        required
                    />
                </div>

                <div class="form-control w-full mb-4">
                    <label class="label" for="injury-date">
                        <span class="label-text font-bold uppercase text-xs tracking-wider">Injury Date</span>
                    </label>
                    <input
                        id="injury-date"
                        type="date"
                        class="input input-bordered w-full rounded-xl"
                        bind:value={newInjury.injuryDate}
                        max={new Date().toISOString().split("T")[0]}
                        required
                    />
                </div>

                <div class="form-control w-full mb-6">
                    <label class="label" for="injury-notes">
                        <span class="label-text font-bold uppercase text-xs tracking-wider">Notes</span>
                    </label>
                    <textarea
                        id="injury-notes"
                        placeholder="Describe how the injury occurred, symptoms, etc."
                        class="textarea textarea-bordered w-full h-24 rounded-xl"
                        bind:value={newInjury.notes}
                    ></textarea>
                </div>

                <div class="modal-action">
                    <button type="button" class="btn btn-outline rounded-xl" on:click={closeCreateInjuryModal}>Cancel</button>
                    <button type="submit" class="btn btn-primary rounded-xl font-bold uppercase">Create Injury</button>
                </div>
            </form>
        </div>
        <div class="modal-backdrop bg-base-300/80" on:click={closeCreateInjuryModal} on:keydown={(e) => e.key === 'Escape' && closeCreateInjuryModal()} role="button" tabindex="0" aria-label="Close modal"></div>
    </div>
{/if}

<!-- Add Event Modal -->
{#if addEventModalOpen && selectedInjury}
    <div class="modal modal-open">
        <div class="modal-box w-11/12 max-w-lg">
            <button class="btn btn-sm btn-circle btn-ghost absolute right-2 top-2" on:click={closeAddEventModal}>✕</button>
            
            <h3 class="font-bold text-2xl mb-2">Add Injury Event</h3>
            <p class="text-sm text-base-content/70 mb-1">
                Recording event for: <strong>{selectedInjury.name}</strong>
            </p>
            <p class="text-sm text-base-content/70 mb-6">
                Track treatments, check-ins, and pain levels
            </p>

            <form on:submit|preventDefault={addInjuryEvent}>
                <div class="form-control w-full mb-4">
                    <label class="label" for="event-type">
                        <span class="label-text font-bold uppercase text-xs tracking-wider">Event Type</span>
                    </label>
                    <select
                        id="event-type"
                        class="select select-bordered w-full rounded-xl"
                        bind:value={newEvent.injuryType}
                        required
                    >
                        <option value={InjuryEventType._0}>Treatment</option>
                        <option value={InjuryEventType._1}>Check-in</option>
                    </select>
                </div>

                <div class="form-control w-full mb-4">
                    <label class="label" for="pain-level">
                        <span class="label-text font-bold uppercase text-xs tracking-wider">
                            Pain Level: <span class="text-lg {getPainLevelColor(newEvent.painLevel)}">{newEvent.painLevel}/10</span>
                        </span>
                    </label>
                    <input
                        id="pain-level"
                        type="range"
                        min="0"
                        max="10"
                        class="range range-primary"
                        bind:value={newEvent.painLevel}
                    />
                    <div class="w-full flex justify-between text-xs px-2 mt-1 font-bold">
                        <span>0</span>
                        <span>2</span>
                        <span>4</span>
                        <span>6</span>
                        <span>8</span>
                        <span>10</span>
                    </div>
                </div>

                <div class="form-control w-full mb-6">
                    <label class="label" for="event-notes">
                        <span class="label-text font-bold uppercase text-xs tracking-wider">Notes</span>
                    </label>
                    <textarea
                        id="event-notes"
                        placeholder="Describe treatment received, symptoms, progress, etc."
                        class="textarea textarea-bordered w-full h-24 rounded-xl"
                        bind:value={newEvent.notes}
                    ></textarea>
                </div>

                <div class="modal-action">
                    <button type="button" class="btn btn-outline rounded-xl" on:click={closeAddEventModal}>Cancel</button>
                    <button type="submit" class="btn btn-primary rounded-xl font-bold uppercase">Add Event</button>
                </div>
            </form>
        </div>
        <div class="modal-backdrop bg-base-300/80" on:click={closeAddEventModal} on:keydown={(e) => e.key === 'Escape' && closeAddEventModal()} role="button" tabindex="0" aria-label="Close modal"></div>
    </div>
{/if}

<!-- Edit Injury Modal -->
{#if editInjuryModalOpen && selectedInjury}
    <div class="modal modal-open">
        <div class="modal-box w-11/12 max-w-lg">
            <button class="btn btn-sm btn-circle btn-ghost absolute right-2 top-2" on:click={closeEditInjuryModal}>✕</button>
            
            <h3 class="font-bold text-2xl mb-2">Edit Injury</h3>
            <p class="text-sm text-base-content/70 mb-6">
                Update injury details and status
            </p>

            <form on:submit|preventDefault={updateInjury}>
                <div class="form-control w-full mb-4">
                    <label class="label" for="edit-injury-name">
                        <span class="label-text font-bold uppercase text-xs tracking-wider">Injury Name</span>
                    </label>
                    <input
                        id="edit-injury-name"
                        type="text"
                        class="input input-bordered w-full rounded-xl"
                        bind:value={editForm.name}
                        required
                    />
                </div>

                <div class="form-control w-full mb-4">
                    <label class="label cursor-pointer justify-start gap-4">
                        <input
                            type="checkbox"
                            class="toggle toggle-error"
                            bind:checked={editForm.isActive}
                        />
                        <span class="label-text font-bold uppercase text-xs tracking-wider">
                            {editForm.isActive ? "Currently Active" : "Recovered"}
                        </span>
                    </label>
                </div>

                <div class="form-control w-full mb-6">
                    <label class="label" for="edit-injury-notes">
                        <span class="label-text font-bold uppercase text-xs tracking-wider">Notes</span>
                    </label>
                    <textarea
                        id="edit-injury-notes"
                        class="textarea textarea-bordered w-full h-24 rounded-xl"
                        bind:value={editForm.notes}
                    ></textarea>
                </div>

                <div class="modal-action">
                    <button type="button" class="btn btn-outline rounded-xl" on:click={closeEditInjuryModal}>Cancel</button>
                    <button type="submit" class="btn btn-primary rounded-xl font-bold uppercase">Save Changes</button>
                </div>
            </form>
        </div>
        <div class="modal-backdrop bg-base-300/80" on:click={closeEditInjuryModal} on:keydown={(e) => e.key === 'Escape' && closeEditInjuryModal()} role="button" tabindex="0" aria-label="Close modal"></div>
    </div>
{/if}

<!-- View Injury Details Modal -->
{#if viewInjuryModalOpen && selectedInjury}
    <div class="modal modal-open">
        <div class="modal-box w-11/12 max-w-4xl max-h-[90vh]">
            <button class="btn btn-sm btn-circle btn-ghost absolute right-2 top-2" on:click={closeViewInjuryModal}>✕</button>
            
            <!-- Injury Header -->
            <div class="mb-6">
                <div class="flex items-start justify-between mb-2">
                    <h3 class="font-bold text-3xl">{selectedInjury.name}</h3>
                    {#if selectedInjury.isActive}
                        <span class="badge badge-error">Active</span>
                    {:else}
                        <span class="badge badge-success">Recovered</span>
                    {/if}
                </div>
                <p class="text-sm text-base-content/50 uppercase tracking-wider font-bold mb-2">
                    Injury Date: {formatDate(selectedInjury.injuryDate)}
                </p>
                {#if selectedInjury.notes}
                    <p class="text-base-content/70">{selectedInjury.notes}</p>
                {/if}
            </div>

            <div class="divider"></div>

            <!-- Events Timeline -->
            <div class="mb-4">
                <h4 class="font-bold text-xl mb-4 uppercase tracking-wider">Event History</h4>
                
                {#if selectedInjury.injuryEvents.length === 0}
                    <div class="text-center py-8 text-base-content/50">
                        <svg
                            xmlns="http://www.w3.org/2000/svg"
                            fill="none"
                            viewBox="0 0 24 24"
                            stroke-width="1.5"
                            stroke="currentColor"
                            class="w-12 h-12 mx-auto mb-2"
                        >
                            <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
                        </svg>
                        <p>No events recorded yet</p>
                        <button
                            on:click={() => {
                                closeViewInjuryModal();
                                if (selectedInjury) openAddEventModal(selectedInjury);
                            }}
                            class="btn btn-sm btn-primary rounded-xl mt-4 font-bold uppercase"
                        >
                            Add First Event
                        </button>
                    </div>
                {:else}
                    <div class="space-y-4 max-h-96 overflow-y-auto pr-2">
                        {#each [...selectedInjury.injuryEvents].sort((a, b) => 
                            new Date(b.creationTime).getTime() - new Date(a.creationTime).getTime()
                        ) as event}
                            <div class="card bg-base-200 p-4 rounded-xl">
                                <div class="flex items-start justify-between mb-2">
                                    <div class="flex items-center gap-2">
                                        <span class="badge {getEventTypeColor(event.injuryType)} font-bold uppercase text-xs">
                                            {getEventTypeLabel(event.injuryType)}
                                        </span>
                                        <span class="text-xs text-base-content/50 font-bold uppercase tracking-wider">
                                            {formatDateTime(event.creationTime)}
                                        </span>
                                    </div>
                                    <button
                                        on:click={() => deleteEvent(event.injuryEventID)}
                                        class="btn btn-ghost btn-xs btn-square text-error"
                                        title="Delete event"
                                    >
                                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-4 h-4">
                                            <path stroke-linecap="round" stroke-linejoin="round" d="m14.74 9-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 0 1-2.244 2.077H8.084a2.25 2.25 0 0 1-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 0 0-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 0 1 3.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 0 0-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 0 0-7.5 0" />
                                        </svg>
                                    </button>
                                </div>
                                
                                <div class="flex items-center gap-2 mb-2">
                                    <span class="text-sm font-bold text-base-content/60">Pain Level:</span>
                                    <span class="text-2xl font-bold {getPainLevelColor(event.painLevel)}">
                                        {event.painLevel}/10
                                    </span>
                                </div>
                                
                                {#if event.notes}
                                    <p class="text-sm text-base-content/70">{event.notes}</p>
                                {/if}
                            </div>
                        {/each}
                    </div>
                {/if}
            </div>

            <div class="modal-action">
                <button
                    on:click={() => {
                        closeViewInjuryModal();
                        if (selectedInjury) openAddEventModal(selectedInjury);
                    }}
                    class="btn btn-primary rounded-xl font-bold uppercase"
                >
                    Add Event
                </button>
                <button class="btn btn-outline rounded-xl" on:click={closeViewInjuryModal}>Close</button>
            </div>
        </div>
        <div class="modal-backdrop bg-base-300/80" on:click={closeViewInjuryModal} on:keydown={(e) => e.key === 'Escape' && closeViewInjuryModal()} role="button" tabindex="0" aria-label="Close modal"></div>
    </div>
{/if}
