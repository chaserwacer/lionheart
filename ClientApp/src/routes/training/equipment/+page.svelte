<script lang="ts">
    import { goto } from '$app/navigation';
    import { onMount } from 'svelte';
    import {
        equipments,
        movementBases,
        muscleGroups,
        fetchEquipments,
        fetchMovementBases,
        fetchMuscleGroups,
        createEquipment,
        updateEquipment,
        deleteEquipment,
        createMovementBase,
        updateMovementBase,
        deleteMovementBase,
        trainingError,
    } from '$lib/stores/trainingStore';
    import type { EquipmentDTO, MovementBaseDTO, ITrainedMuscle } from '$lib/api/ApiClient';

    let activeTab: 'equipment' | 'movements' = 'equipment';
    let isSubmitting = false;

    // Equipment modal states
    let equipmentModalOpen = false;
    let editingEquipment: EquipmentDTO | null = null;
    let equipmentForm = { name: '' };

    // Movement base modal states
    let movementBaseModalOpen = false;
    let editingMovementBase: MovementBaseDTO | null = null;
    let movementBaseForm = {
        name: '',
        description: '',
        trainedMuscles: [] as { muscleGroupID: string; contributionPercentage: number }[]
    };

    // Delete confirmation modal
    let deleteModalOpen = false;
    let deleteTarget: { type: 'equipment' | 'movementBase'; id: string; name: string } | null = null;

    onMount(async () => {
        await Promise.all([fetchEquipments(), fetchMovementBases(), fetchMuscleGroups()]);
    });

    function goBack() {
        goto('/training');
    }

    // Equipment handlers
    function openAddEquipmentModal() {
        editingEquipment = null;
        equipmentForm = { name: '' };
        equipmentModalOpen = true;
    }

    function openEditEquipmentModal(equipment: EquipmentDTO) {
        if (!equipment.equipmentID) return;
        editingEquipment = equipment;
        equipmentForm = { name: equipment.name ?? '' };
        equipmentModalOpen = true;
    }

    function closeEquipmentModal() {
        equipmentModalOpen = false;
        editingEquipment = null;
        equipmentForm = { name: '' };
    }

    async function handleEquipmentSubmit() {
        if (!equipmentForm.name.trim() || isSubmitting) return;
        isSubmitting = true;

        try {
            if (editingEquipment?.equipmentID) {
                await updateEquipment(
                    editingEquipment.equipmentID,
                    equipmentForm.name.trim(),
                    editingEquipment.enabled ?? true
                );
            } else {
                await createEquipment(equipmentForm.name.trim());
            }
            closeEquipmentModal();
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
        editingMovementBase = null;
        movementBaseForm = { name: '', description: '', trainedMuscles: [] };
        movementBaseModalOpen = true;
    }

    function openEditMovementBaseModal(movementBase: MovementBaseDTO) {
        if (!movementBase.movementBaseID) return;
        editingMovementBase = movementBase;
        movementBaseForm = {
            name: movementBase.name ?? '',
            description: movementBase.description ?? '',
            trainedMuscles: movementBase.trainedMuscles?.map(tm => ({
                muscleGroupID: tm.muscleGroupID,
                contributionPercentage: tm.contributionPercentage ?? 0.5
            })) ?? []
        };
        movementBaseModalOpen = true;
    }

    function closeMovementBaseModal() {
        movementBaseModalOpen = false;
        editingMovementBase = null;
        movementBaseForm = { name: '', description: '', trainedMuscles: [] };
    }

    function addMuscleToForm() {
        if ($muscleGroups.length === 0) return;
        const usedIds = movementBaseForm.trainedMuscles.map(tm => tm.muscleGroupID);
        const available = $muscleGroups.find(mg => !usedIds.includes(mg.muscleGroupID));
        if (available) {
            movementBaseForm.trainedMuscles = [
                ...movementBaseForm.trainedMuscles,
                { muscleGroupID: available.muscleGroupID, contributionPercentage: 1 }
            ];
        }
    }

    function removeMuscleFromForm(index: number) {
        movementBaseForm.trainedMuscles = movementBaseForm.trainedMuscles.filter((_, i) => i !== index);
    }

    async function handleMovementBaseSubmit() {
        if (!movementBaseForm.name.trim() || isSubmitting) return;
        isSubmitting = true;

        try {
            const trainedMuscles: ITrainedMuscle[] = movementBaseForm.trainedMuscles.map(tm => ({
                muscleGroupID: tm.muscleGroupID,
                contributionPercentage: tm.contributionPercentage
            }));

            if (editingMovementBase?.movementBaseID) {
                await updateMovementBase(
                    editingMovementBase.movementBaseID,
                    movementBaseForm.name.trim(),
                    movementBaseForm.description.trim() || undefined,
                    trainedMuscles.length > 0 ? trainedMuscles : undefined
                );
            } else {
                await createMovementBase(
                    movementBaseForm.name.trim(),
                    movementBaseForm.description.trim() || undefined,
                    trainedMuscles.length > 0 ? trainedMuscles : undefined
                );
            }
            closeMovementBaseModal();
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
            let success = false;
            if (deleteTarget.type === 'equipment') {
                success = await deleteEquipment(deleteTarget.id);
            } else {
                success = await deleteMovementBase(deleteTarget.id);
            }
            if (success) {
                closeDeleteModal();
            }
        } finally {
            isSubmitting = false;
        }
    }

    function getMuscleGroupName(id: string): string {
        return $muscleGroups.find(mg => mg.muscleGroupID === id)?.name ?? 'Unknown';
    }
</script>

<svelte:head>
    <title>Equipment Manager - Lionheart</title>
</svelte:head>

<div class="min-h-full bg-base-200">
    <div class="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <!-- Header -->
        <header class="mb-8">
            <button
                on:click={goBack}
                class="flex items-center gap-2 text-base-content/50 hover:text-base-content transition-colors mb-4"
            >
                <span>&larr;</span>
                <span class="text-sm font-mono uppercase tracking-widest">Back to Training</span>
            </button>

            <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                <div>
                    <h1 class="text-5xl sm:text-6xl font-display font-black tracking-tightest text-base-content leading-none">
                        LIBRARY
                    </h1>
                    <p class="text-sm font-mono uppercase tracking-widest text-base-content/50 mt-3">
                        Equipment & Movement Bases
                    </p>
                </div>

                <div class="flex items-center gap-2">
                    {#if activeTab === 'equipment'}
                        <button class="btn btn-primary px-5 rounded-xl" on:click={openAddEquipmentModal}>
                            Add Equipment
                        </button>
                    {:else}
                        <button class="btn btn-primary px-5 rounded-xl" on:click={openAddMovementBaseModal}>
                            Add Movement Base
                        </button>
                    {/if}
                </div>
            </div>
        </header>

        <!-- Error Display -->
        {#if $trainingError}
            <div class="alert alert-error mb-6 rounded-xl">
                <span>{$trainingError}</span>
                <button class="btn btn-sm btn-ghost" on:click={() => trainingError.set(null)}>✕</button>
            </div>
        {/if}

        <!-- Tabs -->
        <div class="flex gap-2 mb-6">
            <button
                class="btn rounded-xl {activeTab === 'equipment' ? 'btn-primary' : 'btn-ghost border-2 border-base-content/10'}"
                on:click={() => activeTab = 'equipment'}
            >
                Equipment
                <span class="badge badge-sm ml-2">{$equipments.length}</span>
            </button>
            <button
                class="btn rounded-xl {activeTab === 'movements' ? 'btn-primary' : 'btn-ghost border-2 border-base-content/10'}"
                on:click={() => activeTab = 'movements'}
            >
                Movement Bases
                <span class="badge badge-sm ml-2">{$movementBases.length}</span>
            </button>
        </div>

        <!-- Equipment Tab -->
        {#if activeTab === 'equipment'}
            <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10">
                {#if $equipments.length === 0}
                    <div class="p-8 text-center">
                        <h3 class="text-xl font-bold mb-2">No Equipment</h3>
                        <p class="text-base-content/50 mb-4">Add your first piece of equipment to get started.</p>
                        <button class="btn btn-primary px-6 rounded-xl" on:click={openAddEquipmentModal}>
                            Add Equipment
                        </button>
                    </div>
                {:else}
                    <div class="divide-y divide-base-content/10">
                        {#each $equipments as equipment}
                            <div class="p-4 flex items-center justify-between hover:bg-base-200/50 transition-colors">
                                <div class="flex items-center gap-3">
                                    <h3 class="font-bold">{equipment.name}</h3>
                                    {#if equipment.enabled === false}
                                        <span class="badge badge-ghost badge-sm">Disabled</span>
                                    {/if}
                                </div>
                                <div class="flex items-center gap-2">
                                    <button
                                        class="btn btn-ghost btn-sm rounded-xl"
                                        on:click={() => openEditEquipmentModal(equipment)}
                                    >
                                        Edit
                                    </button>
                                    <button
                                        class="btn btn-ghost btn-sm rounded-xl text-error"
                                        on:click={() => confirmDeleteEquipment(equipment)}
                                    >
                                        Delete
                                    </button>
                                </div>
                            </div>
                        {/each}
                    </div>
                {/if}
            </div>
        {/if}

        <!-- Movement Bases Tab -->
        {#if activeTab === 'movements'}
            <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10">
                {#if $movementBases.length === 0}
                    <div class="p-8 text-center">
                        <h3 class="text-xl font-bold mb-2">No Movement Bases</h3>
                        <p class="text-base-content/50 mb-4">Add your first movement base (e.g., Squat, Bench Press) to get started.</p>
                        <button class="btn btn-primary px-6 rounded-xl" on:click={openAddMovementBaseModal}>
                            Add Movement Base
                        </button>
                    </div>
                {:else}
                    <div class="divide-y divide-base-content/10">
                        {#each $movementBases as movementBase}
                            <div class="p-4 flex items-center justify-between hover:bg-base-200/50 transition-colors">
                                <div class="flex-1 min-w-0">
                                    <h3 class="font-bold">{movementBase.name}</h3>
                                    {#if movementBase.description}
                                        <p class="text-sm text-base-content/50">{movementBase.description}</p>
                                    {/if}
                                    {#if movementBase.trainedMuscles && movementBase.trainedMuscles.length > 0}
                                        <div class="flex flex-wrap gap-1 mt-2">
                                            {#each movementBase.trainedMuscles as muscle}
                                                <span class="badge badge-ghost badge-sm">
                                                    {getMuscleGroupName(muscle.muscleGroupID)}
                                                    {#if muscle.contributionPercentage && muscle.contributionPercentage !== 100}
                                                        ({muscle.contributionPercentage}%)
                                                    {/if}
                                                </span>
                                            {/each}
                                        </div>
                                    {/if}
                                </div>
                                <div class="flex items-center gap-2 ml-4">
                                    <button
                                        class="btn btn-ghost btn-sm rounded-xl"
                                        on:click={() => openEditMovementBaseModal(movementBase)}
                                    >
                                        Edit
                                    </button>
                                    <button
                                        class="btn btn-ghost btn-sm rounded-xl text-error"
                                        on:click={() => confirmDeleteMovementBase(movementBase)}
                                    >
                                        Delete
                                    </button>
                                </div>
                            </div>
                        {/each}
                    </div>
                {/if}
            </div>
        {/if}

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
                            {editingEquipment ? 'Edit Equipment' : 'Add Equipment'}
                        </h3>
                        <p class="text-xs font-mono uppercase tracking-widest text-base-content/50 mt-1">
                            {editingEquipment ? 'Update equipment details' : 'Create new equipment'}
                        </p>
                    </div>
                    <button on:click={closeEquipmentModal} class="btn btn-ghost btn-sm btn-circle">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>
            </div>

            <form on:submit|preventDefault={handleEquipmentSubmit} class="p-6">
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
                    <button type="button" class="btn btn-ghost px-5 rounded-xl" on:click={closeEquipmentModal}>
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
                            {editingEquipment ? 'Save' : 'Create'}
                        {/if}
                    </button>
                </div>
            </form>
        </div>
        <div
            class="modal-backdrop bg-base-300/80"
            on:click={closeEquipmentModal}
            on:keydown={(e) => e.key === 'Escape' && closeEquipmentModal()}
            role="button"
            tabindex="0"
            aria-label="Close modal"
        ></div>
    </div>
{/if}

<!-- Movement Base Modal -->
{#if movementBaseModalOpen}
    <div class="modal modal-open">
        <div class="modal-box w-11/12 max-w-lg bg-base-100 p-0 overflow-hidden border-2 border-base-content/20">
            <div class="p-6 pb-4 border-b-2 border-base-content/10">
                <div class="flex items-center justify-between">
                    <div>
                        <h3 class="text-2xl font-display font-black tracking-tight">
                            {editingMovementBase ? 'Edit Movement Base' : 'Add Movement Base'}
                        </h3>
                        <p class="text-xs font-mono uppercase tracking-widest text-base-content/50 mt-1">
                            {editingMovementBase ? 'Update movement pattern' : 'Create new movement pattern'}
                        </p>
                    </div>
                    <button on:click={closeMovementBaseModal} class="btn btn-ghost btn-sm btn-circle">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-5 h-5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>
            </div>

            <form on:submit|preventDefault={handleMovementBaseSubmit} class="p-6 space-y-4">
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

                <!-- Trained Muscles Section -->
                <div class="form-control w-full">
                    <div class="flex items-center justify-between mb-2">
                        <div class="label p-0">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Trained Muscles</span>
                        </div>
                        <button
                            type="button"
                            class="btn btn-ghost btn-xs rounded-lg"
                            on:click={addMuscleToForm}
                            disabled={movementBaseForm.trainedMuscles.length >= $muscleGroups.length}
                        >
                            + Add
                        </button>
                    </div>

                    {#if movementBaseForm.trainedMuscles.length > 0}
                        <div class="space-y-2">
                            {#each movementBaseForm.trainedMuscles as muscle, index}
                                <div class="flex items-center gap-2 p-3 bg-base-200 rounded-xl">
                                    <select
                                        class="select select-bordered select-sm flex-1 rounded-lg"
                                        bind:value={muscle.muscleGroupID}
                                    >
                                        {#each $muscleGroups as mg}
                                            <option value={mg.muscleGroupID}>{mg.name}</option>
                                        {/each}
                                    </select>
                                    <div class="flex items-center gap-1">
                                        <input
                                            type="number"
                                            min="0"
                                            max="1"
                                            placeholder=".5"
                                            step="0.5"
                                            class="input input-bordered input-sm w-16 rounded-lg text-center"
                                            bind:value={muscle.contributionPercentage}
                                        />
                                    </div>
                                    <button
                                        type="button"
                                        class="btn btn-ghost btn-sm btn-circle text-error"
                                        on:click={() => removeMuscleFromForm(index)}
                                    >
                                        ✕
                                    </button>
                                </div>
                            {/each}
                        </div>
                    {:else}
                        <p class="text-xs text-base-content/50 italic">No muscles assigned. Click "Add" to track muscle involvement.</p>
                    {/if}
                </div>

                <div class="flex justify-end gap-2 pt-4">
                    <button type="button" class="btn btn-ghost px-5 rounded-xl" on:click={closeMovementBaseModal}>
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
                            {editingMovementBase ? 'Save' : 'Create'}
                        {/if}
                    </button>
                </div>
            </form>
        </div>
        <div
            class="modal-backdrop bg-base-300/80"
            on:click={closeMovementBaseModal}
            on:keydown={(e) => e.key === 'Escape' && closeMovementBaseModal()}
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
                    <button on:click={closeDeleteModal} class="btn btn-ghost btn-sm btn-circle">
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
                    <button class="btn btn-ghost px-5 rounded-xl" on:click={closeDeleteModal}>
                        Cancel
                    </button>
                    <button
                        class="btn btn-error px-5 rounded-xl"
                        on:click={handleDelete}
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
            on:click={closeDeleteModal}
            on:keydown={(e) => e.key === 'Escape' && closeDeleteModal()}
            role="button"
            tabindex="0"
            aria-label="Close modal"
        ></div>
    </div>
{/if}
