<script lang="ts">
    import { goto } from '$app/navigation';
    import { onMount } from 'svelte';
    import { training } from '$lib/stores/training.svelte';
    import type { EquipmentDTO, MovementBaseDTO } from '$lib/api/ApiClient';

    const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

    let isSubmitting = $state(false);

    let showEquipmentList = $state(true);
    let showMovementBaseList = $state(true);

    let editingEquipmentId = $state<string | null>(null);
    let equipmentDraftName = $state('');
    let equipmentDraftEnabled = $state(true);

    let editingMovementBaseId = $state<string | null>(null);
    let movementBaseDraftName = $state('');
    let movementBaseDraftDescription = $state('');
    let movementBaseDraftMuscleGroupIDs = $state<string[]>([]);

    // Equipment modal states
    let equipmentModalOpen = $state(false);
    let equipmentForm = $state({ name: '' });

    // Movement base modal states
    let movementBaseModalOpen = $state(false);
    let movementBaseForm = $state({
        name: '',
        description: '',
        muscleGroupIDs: [] as string[]
    });

    // Delete confirmation modal
    let deleteModalOpen = $state(false);
    let deleteTarget = $state<{ type: 'equipment' | 'movementBase'; id: string; name: string } | null>(null);

    onMount(async () => {
        await training.loadLibrary();
    });

    function goBack() {
        goto('/training');
    }

    // Equipment handlers
    function openAddEquipmentModal() {
        equipmentForm = { name: '' };
        equipmentModalOpen = true;
    }

    function closeEquipmentModal() {
        equipmentModalOpen = false;
        equipmentForm = { name: '' };
    }

    async function handleEquipmentSubmit() {
        if (!equipmentForm.name.trim() || isSubmitting) return;
        isSubmitting = true;

        try {
            await training.createEquipment(equipmentForm.name.trim());
            closeEquipmentModal();
        } finally {
            isSubmitting = false;
        }
    }

    function startEditEquipment(equipment: EquipmentDTO) {
        if (!equipment.equipmentID) return;
        editingEquipmentId = equipment.equipmentID;
        equipmentDraftName = equipment.name ?? '';
        equipmentDraftEnabled = equipment.enabled ?? true;
    }

    function cancelEditEquipment() {
        editingEquipmentId = null;
        equipmentDraftName = '';
        equipmentDraftEnabled = true;
    }

    async function saveEditEquipment(equipment: EquipmentDTO) {
        if (!equipment.equipmentID || isSubmitting) return;
        if (!equipmentDraftName.trim()) return;
        isSubmitting = true;
        try {
            await training.updateEquipment(equipment.equipmentID, equipmentDraftName.trim(), equipmentDraftEnabled);
            cancelEditEquipment();
        } finally {
            isSubmitting = false;
        }
    }

    function confirmDeleteEquipment(equipment: EquipmentDTO) {
        if (!equipment.equipmentID) return;
        deleteTarget = {
            type: 'equipment',
            id: equipment.equipmentID,
            name: equipment.name ?? 'Unknown'
        };
        deleteModalOpen = true;
    }

    // Movement base handlers
    function openAddMovementBaseModal() {
        movementBaseForm = { name: '', description: '', muscleGroupIDs: [] };
        movementBaseModalOpen = true;
    }

    function closeMovementBaseModal() {
        movementBaseModalOpen = false;
        movementBaseForm = { name: '', description: '', muscleGroupIDs: [] };
    }

    async function handleMovementBaseSubmit() {
        if (!movementBaseForm.name.trim() || isSubmitting) return;
        isSubmitting = true;

        try {
            const selectedMuscleGroups = training.muscleGroups.filter(mg =>
                movementBaseForm.muscleGroupIDs.includes(mg.muscleGroupID)
            );

            await training.createMovementBase(
                movementBaseForm.name.trim(),
                movementBaseForm.description.trim() || "",
                selectedMuscleGroups.length > 0 ? selectedMuscleGroups : []
            );
            closeMovementBaseModal();
        } finally {
            isSubmitting = false;
        }
    }

    function startEditMovementBase(movementBase: MovementBaseDTO) {
        if (!movementBase.movementBaseID) return;
        editingMovementBaseId = movementBase.movementBaseID;
        movementBaseDraftName = movementBase.name ?? '';
        movementBaseDraftDescription = movementBase.description ?? '';
        movementBaseDraftMuscleGroupIDs = (movementBase.muscleGroups ?? [])
            .map(mg => mg.muscleGroupID)
            .filter(Boolean);
    }

    function cancelEditMovementBase() {
        editingMovementBaseId = null;
        movementBaseDraftName = '';
        movementBaseDraftDescription = '';
        movementBaseDraftMuscleGroupIDs = [];
    }

    async function saveEditMovementBase(movementBase: MovementBaseDTO) {
        if (!movementBase.movementBaseID || isSubmitting) return;
        if (!movementBaseDraftName.trim()) return;
        isSubmitting = true;
        try {
            const selectedMuscleGroups = training.muscleGroups.length > 0
                ? training.muscleGroups.filter(mg => movementBaseDraftMuscleGroupIDs.includes(mg.muscleGroupID))
                : (movementBase.muscleGroups ?? []);

            await training.updateMovementBase(
                movementBase.movementBaseID,
                movementBaseDraftName.trim(),
                movementBaseDraftDescription.trim() || "",
                selectedMuscleGroups.length > 0 ? selectedMuscleGroups : []
            );
            cancelEditMovementBase();
        } finally {
            isSubmitting = false;
        }
    }

    function confirmDeleteMovementBase(movementBase: MovementBaseDTO) {
        if (!movementBase.movementBaseID) return;
        deleteTarget = {
            type: 'movementBase',
            id: movementBase.movementBaseID,
            name: movementBase.name ?? 'Unknown'
        };
        deleteModalOpen = true;
    }

    // Delete handlers
    function closeDeleteModal() {
        deleteModalOpen = false;
        deleteTarget = null;
    }

    async function handleDelete() {
        if (!deleteTarget || !deleteTarget.id || isSubmitting) return;
        isSubmitting = true;

        try {
            if (deleteTarget.type === 'equipment') {
                await training.deleteEquipment(deleteTarget.id);
            } else {
                await training.deleteMovementBase(deleteTarget.id);
            }
            closeDeleteModal();
        } finally {
            isSubmitting = false;
        }
    }

</script>

<svelte:head>
    <title>Equipment & Movement Base Catalogue - Lionheart</title>
</svelte:head>

<div class="min-h-full bg-base-200">
    <div class="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <!-- Header -->
        <header class="mb-8">
            <button
                onclick={goBack}
                class="flex items-center gap-2 text-base-content/50 hover:text-base-content transition-colors mb-4"
            >
                <span>&larr;</span>
                <span class="text-sm font-mono uppercase tracking-widest">Back to Training</span>
            </button>

            <div class="flex flex-col sm:flex-col sm:items-left sm:justify-between gap-4 ">
                <div>
                    <h1 class="text-4xl sm:text-5xl font-display font-black tracking-tight text-base-content leading-tight">
                        Equipment & Movement Base Catalogue
                    </h1>
                    <p class="text-base-content/60 text-sm sm:text-base mt-2">
                        A clean reference list for your training library.
                    </p>
                </div>
                <div class="divider p-0 m-0"></div>
                <div class="flex items-center gap-8">
                    <button class="btn btn-white px-5 btn-outline btn-lg outline" onclick={openAddEquipmentModal}>
                        Add Equipment
                    </button>
                    <button class="btn btn-white px-5 btn-outline btn-lg outline" onclick={openAddMovementBaseModal}>
                        Add Movement Base
                    </button>
                </div>
            </div>
        </header>

        <!-- Error Display -->
        {#if training.error}
            <div class="alert alert-error mb-6 rounded-xl">
                <span>{training.error}</span>
                <button class="btn btn-sm btn-ghost" onclick={() => (training.error = "")}>✕</button>
            </div>
        {/if}

        <!-- Tabs -->
        <!-- Visibility toggles (when a list is closed) -->
        {#if !showEquipmentList || !showMovementBaseList}
            <div class="flex flex-wrap gap-2 mb-6">
                {#if !showEquipmentList}
                    <button class="btn btn-sm rounded-xl" onclick={() => (showEquipmentList = true)}>
                        Show Equipment
                        <span class="badge badge-sm ml-2">{training.equipment.length}</span>
                    </button>
                {/if}
                {#if !showMovementBaseList}
                    <button class="btn btn-sm rounded-xl" onclick={() => (showMovementBaseList = true)}>
                        Show Movement Bases
                        <span class="badge badge-sm ml-2">{training.movementBases.length}</span>
                    </button>
                {/if}
            </div>
        {/if}

        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            {#if showEquipmentList}
                <ul class="list bg-base-100 rounded-box shadow-md">
                    <li class="p-4 pb-2 text-xs opacity-60 tracking-wide flex items-center justify-between">
                        <span>Equipment</span>
                        <div class="flex items-center gap-2">
                            <button class="btn btn-xs btn-ghost" onclick={openAddEquipmentModal}>Add</button>
                            <button
                                class="btn btn-xs btn-square btn-ghost"
                                aria-label="Close equipment list"
                                onclick={() => {
                                    showEquipmentList = false;
                                    cancelEditEquipment();
                                }}
                            >
                                <svg class="size-[1.1em]" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <path d="M18 6 6 18"></path>
                                    <path d="m6 6 12 12"></path>
                                </svg>
                            </button>
                        </div>
                    </li>

                    {#if training.equipment.length === 0}
                        <li class="p-4 text-sm text-base-content/60">No equipment yet.</li>
                    {:else}
                        {#each training.equipment as equipment (equipment.equipmentID)}
                            <li class="list-row">
                                <div class="size-10 rounded-box bg-base-200 flex items-center justify-center font-mono text-xs text-base-content/60">
                                    EQ
                                </div>
                                <div class="min-w-0">
                                    {#if editingEquipmentId === equipment.equipmentID}
                                        <input
                                            class="input input-sm input-bordered w-full"
                                            bind:value={equipmentDraftName}
                                            placeholder="Equipment name"
                                        />
                                        <label class="label cursor-pointer px-0 py-1">
                                            <span class="label-text text-xs opacity-60">Enabled</span>
                                            <input type="checkbox" class="toggle toggle-sm" bind:checked={equipmentDraftEnabled} />
                                        </label>
                                    {:else}
                                        <div class="font-semibold truncate">{equipment.name}</div>
                                        <div class="text-xs uppercase font-semibold opacity-60">
                                            {equipment.enabled === false ? 'Disabled' : 'Enabled'}
                                        </div>
                                    {/if}
                                </div>

                                {#if editingEquipmentId === equipment.equipmentID}
                                    <button
                                        class="btn btn-square btn-ghost"
                                        aria-label="Save equipment"
                                        disabled={isSubmitting}
                                        onclick={() => saveEditEquipment(equipment)}
                                    >
                                        <svg class="size-[1.2em]" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                            <path d="M20 6 9 17l-5-5" />
                                        </svg>
                                    </button>
                                    <button
                                        class="btn btn-square btn-ghost"
                                        aria-label="Cancel edit"
                                        disabled={isSubmitting}
                                        onclick={cancelEditEquipment}
                                    >
                                        <svg class="size-[1.1em]" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                            <path d="M18 6 6 18"></path>
                                            <path d="m6 6 12 12"></path>
                                        </svg>
                                    </button>
                                {:else}
                                    <button
                                        class="btn btn-square btn-ghost"
                                        aria-label="Edit equipment"
                                        disabled={!equipment.equipmentID || isSubmitting}
                                        onclick={() => startEditEquipment(equipment)}
                                    >
                                        <svg class="size-[1.2em]" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                            <path d="M12 20h9" />
                                            <path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z" />
                                        </svg>
                                    </button>
                                    <button
                                        class="btn btn-square btn-ghost text-error"
                                        aria-label="Delete equipment"
                                        disabled={!equipment.equipmentID || isSubmitting}
                                        onclick={() => confirmDeleteEquipment(equipment)}
                                    >
                                        <svg class="size-[1.2em]" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                            <path d="M3 6h18" />
                                            <path d="M8 6V4h8v2" />
                                            <path d="M19 6l-1 14H6L5 6" />
                                            <path d="M10 11v6" />
                                            <path d="M14 11v6" />
                                        </svg>
                                    </button>
                                {/if}
                            </li>
                        {/each}
                    {/if}
                </ul>
            {/if}

            {#if showMovementBaseList}
                <ul class="list bg-base-100 rounded-box shadow-md">
                    <li class="p-4 pb-2 text-xs opacity-60 tracking-wide flex items-center justify-between">
                        <span>Movement Base</span>
                        <div class="flex items-center gap-2">
                            <button class="btn btn-xs btn-ghost" onclick={openAddMovementBaseModal}>Add</button>
                            <button
                                class="btn btn-xs btn-square btn-ghost"
                                aria-label="Close movement base list"
                                onclick={() => {
                                    showMovementBaseList = false;
                                    cancelEditMovementBase();
                                }}
                            >
                                <svg class="size-[1.1em]" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <path d="M18 6 6 18"></path>
                                    <path d="m6 6 12 12"></path>
                                </svg>
                            </button>
                        </div>
                    </li>

                    {#if training.movementBases.length === 0}
                        <li class="p-4 text-sm text-base-content/60">No movement bases yet.</li>
                    {:else}
                        {#each training.movementBases as movementBase, index (movementBase.movementBaseID && movementBase.movementBaseID !== EMPTY_GUID ? movementBase.movementBaseID : `movementBase-temp:${index}`)}
                            <li class="list-row">
                                <div class="size-10 rounded-box bg-base-200 flex items-center justify-center font-mono text-xs text-base-content/60">
                                    MB
                                </div>
                                <div class="min-w-0">
                                    {#if editingMovementBaseId === movementBase.movementBaseID}
                                        <input
                                            class="input input-sm input-bordered w-full"
                                            bind:value={movementBaseDraftName}
                                            placeholder="Movement base name"
                                        />
                                        <textarea
                                            class="textarea textarea-bordered textarea-sm w-full mt-2"
                                            rows="2"
                                            bind:value={movementBaseDraftDescription}
                                            placeholder="Description (optional)"
                                        ></textarea>

                                        <div class="mt-3">
                                            <div class="text-xs uppercase font-semibold opacity-60">Muscle Groups</div>
                                            {#if training.muscleGroups.length === 0}
                                                <div class="text-xs opacity-50 italic mt-1">No muscle groups available.</div>
                                            {:else}
                                                <div class="flex flex-wrap gap-2 mt-2">
                                                    {#each training.muscleGroups as mg (mg.muscleGroupID)}
                                                        <label class="flex items-center gap-2 px-2 py-1 bg-base-200 rounded-lg cursor-pointer">
                                                            <input
                                                                type="checkbox"
                                                                class="checkbox checkbox-xs"
                                                                value={mg.muscleGroupID}
                                                                bind:group={movementBaseDraftMuscleGroupIDs}
                                                            />
                                                            <span class="text-xs font-medium">{mg.name ?? 'Unknown'}</span>
                                                        </label>
                                                    {/each}
                                                </div>
                                            {/if}
                                        </div>
                                    {:else}
                                        <div class="font-semibold truncate">{movementBase.name}</div>
                                        <div class="text-xs font-semibold opacity-60 truncate">
                                            {movementBase.description ?? 'No description'}
                                        </div>
                                        <div class="text-[10px] leading-tight opacity-60 truncate">
                                            Muscle groups:
                                            {#if (movementBase.muscleGroups ?? []).length > 0}
                                                {(movementBase.muscleGroups ?? [])
                                                    .map(mg => mg.name)
                                                    .filter(Boolean)
                                                    .join(' • ') || '—'}
                                            {:else}
                                                —
                                            {/if}
                                        </div>
                                    {/if}
                                </div>

                                {#if editingMovementBaseId === movementBase.movementBaseID}
                                    <button
                                        class="btn btn-square btn-ghost"
                                        aria-label="Save movement base"
                                        disabled={isSubmitting}
                                        onclick={() => saveEditMovementBase(movementBase)}
                                    >
                                        <svg class="size-[1.2em]" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                            <path d="M20 6 9 17l-5-5" />
                                        </svg>
                                    </button>
                                    <button
                                        class="btn btn-square btn-ghost"
                                        aria-label="Cancel edit"
                                        disabled={isSubmitting}
                                        onclick={cancelEditMovementBase}
                                    >
                                        <svg class="size-[1.1em]" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                            <path d="M18 6 6 18"></path>
                                            <path d="m6 6 12 12"></path>
                                        </svg>
                                    </button>
                                {:else}
                                    <button
                                        class="btn btn-square btn-ghost"
                                        aria-label="Edit movement base"
                                        disabled={!movementBase.movementBaseID || isSubmitting}
                                        onclick={() => startEditMovementBase(movementBase)}
                                    >
                                        <svg class="size-[1.2em]" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                            <path d="M12 20h9" />
                                            <path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z" />
                                        </svg>
                                    </button>
                                    <button
                                        class="btn btn-square btn-ghost text-error"
                                        aria-label="Delete movement base"
                                        disabled={!movementBase.movementBaseID || isSubmitting}
                                        onclick={() => confirmDeleteMovementBase(movementBase)}
                                    >
                                        <svg class="size-[1.2em]" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                            <path d="M3 6h18" />
                                            <path d="M8 6V4h8v2" />
                                            <path d="M19 6l-1 14H6L5 6" />
                                            <path d="M10 11v6" />
                                            <path d="M14 11v6" />
                                        </svg>
                                    </button>
                                {/if}
                            </li>
                        {/each}
                    {/if}
                </ul>
            {/if}
        </div>

        <!-- Info Card -->
        <div class="mt-8 card bg-base-100 shadow-editorial border-2 border-base-content/10 p-6">
            <h3 class="font-bold mb-3">About the Library</h3>
            <div class="text-sm text-base-content/70 space-y-2">
                <p>
                    <strong>Equipment</strong> represents the tools you use for training (e.g., Barbell, Dumbbell, Cable Machine, Bodyweight).
                </p>
                <p>
                    <strong>Movement Bases</strong> are the fundamental exercise patterns (e.g., Squat, Deadlift, Bench Press, Row).
                </p>
                <p>
                    When you create a movement in a session, you combine a Movement Base with Equipment to define the specific exercise (e.g., Barbell Squat, Dumbbell Bench Press).
                    You then optionally can include a modifier (incline, close-grip, etc.) to further specify the movement.
                </p>
            </div>
        </div>
    </div>
</div>

<!-- Equipment Modal -->
{#if equipmentModalOpen}
    <div class="modal modal-open">
        <div class="modal-box w-11/12 max-w-lg bg-base-100 p-0 overflow-hidden border-2 border-base-content/20">
            <div class="p-6 pb-4 border-b-2 border-base-content/10">
                <div class="flex items-center justify-between">
                    <div>
                        <h3 class="text-2xl font-display font-black tracking-tight">
                            Add Equipment
                        </h3>
                        <p class="text-xs font-mono uppercase tracking-widest text-base-content/50 mt-1">
                            Create new equipment
                        </p>
                    </div>
                    <button onclick={closeEquipmentModal} aria-label="Close" class="btn btn-ghost btn-circle h-11 min-h-11 w-11">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>
            </div>

            <form onsubmit={(event) => { event.preventDefault(); handleEquipmentSubmit(); }} class="p-6">
                <div class="form-control w-full">
                    <label class="label" for="equipment-name">
                        <span class="label-text font-bold uppercase text-xs tracking-wider">Name</span>
                    </label>
                    <input
                        id="equipment-name"
                        type="text"
                        placeholder="e.g., Barbell, Dumbbell, Cable Machine"
                        class="input input-bordered w-full rounded-xl"
                        bind:value={equipmentForm.name}
                        required
                    />
                </div>

                <div class="flex justify-end gap-2 mt-6">
                    <button type="button" class="btn btn-ghost px-5 rounded-xl" onclick={closeEquipmentModal}>
                        Cancel
                    </button>
                    <button
                        type="submit"
                        class="btn btn-primary px-5 rounded-xl"
                        disabled={!equipmentForm.name.trim() || isSubmitting}
                    >
                        {#if isSubmitting}
                            <span class="loading loading-spinner loading-sm"></span>
                        {:else}
                            Create
                        {/if}
                    </button>
                </div>
            </form>
        </div>
        <div
            class="modal-backdrop bg-base-300/80"
            onclick={closeEquipmentModal}
            onkeydown={(e) => e.key === 'Escape' && closeEquipmentModal()}
            role="button"
            tabindex="0"
            aria-label="Close modal"
        ></div>
    </div>
{/if}

<!-- Movement Base Modal -->
{#if movementBaseModalOpen}
    <div class="modal modal-open">
        <div class="modal-box w-11/12 max-w-lg bg-base-100 p-0 overflow-hidden border-2 border-base-content/20 max-h-[calc(100dvh-2rem)] flex flex-col">
            <div class="p-6 pb-4 border-b-2 border-base-content/10">
                <div class="flex items-center justify-between">
                    <div>
                        <h3 class="text-2xl font-display font-black tracking-tight">
                            Add Movement Base
                        </h3>
                        <p class="text-xs font-mono uppercase tracking-widest text-base-content/50 mt-1">
                            Create new movement pattern
                        </p>
                    </div>
                    <button onclick={closeMovementBaseModal} aria-label="Close" class="btn btn-ghost btn-circle h-11 min-h-11 w-11">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>
            </div>

            <form onsubmit={(event) => { event.preventDefault(); handleMovementBaseSubmit(); }} class="flex flex-col min-h-0">
                <div class="p-6 space-y-4 overflow-y-auto min-h-0">
                    <div class="form-control w-full">
                        <label class="label" for="movement-name">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Name</span>
                        </label>
                        <input
                            id="movement-name"
                            type="text"
                            placeholder="e.g., Squat, Bench Press, Deadlift"
                            class="input input-bordered w-full rounded-xl"
                            bind:value={movementBaseForm.name}
                            required
                        />
                    </div>

                    <div class="form-control w-full">
                        <label class="label" for="movement-description">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Description</span>
                        </label>
                        <textarea
                            id="movement-description"
                            placeholder="Brief description of the movement"
                            class="textarea textarea-bordered w-full rounded-xl"
                            rows="2"
                            bind:value={movementBaseForm.description}
                        ></textarea>
                    </div>

                    <!-- Muscle Groups -->
                    <div class="form-control w-full">
                        <div class="label p-0 mb-2">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Muscle Groups (optional)</span>
                        </div>

                        {#if training.muscleGroups.length === 0}
                            <p class="text-xs text-base-content/50 italic">No muscle groups available.</p>
                        {:else}
                            <div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
                                {#each training.muscleGroups as mg (mg.muscleGroupID)}
                                    <label class="flex items-center gap-3 p-3 bg-base-200 rounded-xl cursor-pointer">
                                        <input
                                            type="checkbox"
                                            class="checkbox checkbox-sm"
                                            value={mg.muscleGroupID}
                                            bind:group={movementBaseForm.muscleGroupIDs}
                                        />
                                        <span class="text-sm font-medium">{mg.name}</span>
                                    </label>
                                {/each}
                            </div>
                        {/if}
                    </div>
                </div>

                <div class="flex justify-end gap-2 p-6 pt-4 border-t-2 border-base-content/10 bg-base-100">
                    <button type="button" class="btn btn-ghost px-5 rounded-xl" onclick={closeMovementBaseModal}>
                        Cancel
                    </button>
                    <button
                        type="submit"
                        class="btn btn-primary px-5 rounded-xl"
                        disabled={!movementBaseForm.name.trim() || isSubmitting}
                    >
                        {#if isSubmitting}
                            <span class="loading loading-spinner loading-sm"></span>
                        {:else}
                            Create
                        {/if}
                    </button>
                </div>
            </form>
        </div>
        <div
            class="modal-backdrop bg-base-300/80"
            onclick={closeMovementBaseModal}
            onkeydown={(e) => e.key === 'Escape' && closeMovementBaseModal()}
            role="button"
            tabindex="0"
            aria-label="Close modal"
        ></div>
    </div>
{/if}

<!-- Delete Confirmation Modal -->
{#if deleteModalOpen && deleteTarget}
    <div class="modal modal-open">
        <div class="modal-box w-11/12 max-w-md bg-base-100 p-0 overflow-hidden border-2 border-error/30">
            <div class="p-6 pb-4 border-b-2 border-base-content/10">
                <div class="flex items-center justify-between">
                    <div>
                        <h3 class="text-2xl font-display font-black tracking-tight text-error">Confirm Delete</h3>
                        <p class="text-xs font-mono uppercase tracking-widest text-base-content/50 mt-1">
                            This action cannot be undone
                        </p>
                    </div>
                    <button onclick={closeDeleteModal} aria-label="Close" class="btn btn-ghost btn-circle h-11 min-h-11 w-11">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>
            </div>

            <div class="p-6">
                <p class="text-base-content/70">
                    Are you sure you want to delete <strong class="text-base-content">{deleteTarget.name}</strong>?
                </p>
                {#if deleteTarget.type === 'movementBase'}
                    <p class="text-sm text-warning mt-2">
                        Note: This may affect existing movements that use this movement base.
                    </p>
                {/if}

                <div class="flex justify-end gap-2 mt-6">
                    <button class="btn btn-ghost px-5 rounded-xl" onclick={closeDeleteModal}>
                        Cancel
                    </button>
                    <button
                        class="btn btn-error px-5 rounded-xl"
                        onclick={handleDelete}
                        disabled={isSubmitting}
                    >
                        {#if isSubmitting}
                            <span class="loading loading-spinner loading-sm"></span>
                        {:else}
                            Delete
                        {/if}
                    </button>
                </div>
            </div>
        </div>
        <div
            class="modal-backdrop bg-base-300/80"
            onclick={closeDeleteModal}
            onkeydown={(e) => e.key === 'Escape' && closeDeleteModal()}
            role="button"
            tabindex="0"
            aria-label="Close modal"
        ></div>
    </div>
{/if}
