<script lang="ts">
    import { onMount } from 'svelte';
    import { goto } from '$app/navigation';
    import {
        programs,
        activeProgram,
        lastCompletedSession,
        nextSession,
        recentSessions,
        isLoading,
        trainingError,
        fetchAllTrainingData,
        getStatusText,
        getStatusColor,
        formatSessionDate,
        getSessionTitle,
    } from '$lib/stores/trainingStore';
    import {
        TrainingSessionStatus,
        type TrainingSessionDTO,
        type TrainingProgramDTO,
        type LiftSetEntryDTO,
    } from '$lib/api/ApiClient';

    // Modal states
    let createProgramModalOpen = false;
    let createSessionModalOpen = false;

    // Forms
    let programForm = {
        title: '',
        startDate: new Date().toISOString().slice(0, 10),
        endDate: '',
        tagsCsv: ''
    };

    let sessionForm = {
        date: new Date().toISOString().slice(0, 10),
        notes: ''
    };

    onMount(async () => {
        await fetchAllTrainingData();
    });

    function openCreateProgramModal() {
        programForm = {
            title: '',
            startDate: new Date().toISOString().slice(0, 10),
            endDate: '',
            tagsCsv: ''
        };
        createProgramModalOpen = true;
    }

    function closeCreateProgramModal() {
        createProgramModalOpen = false;
    }

    function openCreateSessionModal() {
        sessionForm = {
            date: new Date().toISOString().slice(0, 10),
            notes: ''
        };
        createSessionModalOpen = true;
    }

    function closeCreateSessionModal() {
        createSessionModalOpen = false;
    }

    function goToSession(session: TrainingSessionDTO) {
        if (session.trainingSessionID) {
            goto(`/training/session/${session.trainingSessionID}`);
        }
    }

    function goToProgram(program: TrainingProgramDTO) {
        if (program.trainingProgramID) {
            goto(`/training/program/${program.trainingProgramID}`);
        }
    }

    function goToEquipmentManager() {
        goto('/training/equipment');
    }

    // Calculate stats for session cards
    function getMovementCount(session: TrainingSessionDTO | null): number {
        return session?.movements?.length ?? 0;
    }

    function getTotalSets(session: TrainingSessionDTO | null): number {
        if (!session?.movements) return 0;
        return session.movements.reduce((sum, m) => {
            const liftSets = m.liftSets?.length ?? 0;
            const dtSets = m.distanceTimeSets?.length ?? 0;
            return sum + liftSets + dtSets;
        }, 0);
    }

    function getTotalVolume(session: TrainingSessionDTO | null): number {
        if (!session?.movements) return 0;
        return session.movements.reduce((sum, m) => {
            const liftVolume = m.liftSets?.reduce(
                (s: number, e: LiftSetEntryDTO) => s + ((e.actualWeight ?? 0) * (e.actualReps ?? 0)),
                0
            ) ?? 0;
            return sum + liftVolume;
        }, 0);
    }

    function formatVolume(vol: number): string {
        if (vol > 1000) return (vol / 1000).toFixed(1) + 'k';
        return vol.toString();
    }

    function formatDate(date: any): string {
        if (!date) return "N/A";
        const d = new Date(date.toString());
        return d.toLocaleDateString("en-US", {
            month: "short",
            day: "numeric",
            timeZone: "UTC"
        });
    }

    function getProgramProgress(program: TrainingProgramDTO | null): { completed: number; total: number } {
        if (!program?.trainingSessions) return { completed: 0, total: 0 };
        const completed = program.trainingSessions.filter(s => s.status === TrainingSessionStatus._2).length;
        return { completed, total: program.trainingSessions.length };
    }
</script>

<svelte:head>
    <title>Training - Lionheart</title>
</svelte:head>

<div class="min-h-full bg-base-200">
    <div class="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <!-- Header -->
        <header class="mb-8">
            <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                <div>
                    <h1 class="text-5xl sm:text-6xl font-display font-black tracking-tightest text-base-content leading-none">
                        TRAINING
                    </h1>
                    <p class="text-sm font-mono uppercase tracking-widest text-base-content/50 mt-3">
                        Programs, Sessions & Progress
                    </p>
                </div>

                <div class="flex items-center gap-2">
                    <button
                        class="btn btn-primary px-5 rounded-xl"
                        on:click={openCreateSessionModal}
                    >
                        New Session
                    </button>
                    <button
                        class="btn btn-outline px-5 rounded-xl"
                        on:click={openCreateProgramModal}
                    >
                        New Program
                    </button>
                </div>
            </div>
        </header>

        {#if $trainingError}
            <div class="alert alert-error mb-6">
                <span>{$trainingError}</span>
            </div>
        {/if}

        {#if $isLoading}
            <div class="flex justify-center items-center py-12">
                <span class="loading loading-spinner loading-lg"></span>
            </div>
        {:else}
            <!-- Overview Cards Grid -->
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 mb-8">
                <!-- Last Session Card -->
                <button
                    on:click={() => $lastCompletedSession && goToSession($lastCompletedSession)}
                    class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
                           cursor-pointer p-6 text-left w-full h-full
                           border-2 border-base-content/10 hover:border-base-content/30
                           {!$lastCompletedSession ? 'opacity-60 cursor-not-allowed' : ''}"
                    disabled={!$lastCompletedSession}
                >
                    <div class="flex items-start justify-between mb-6">
                        <div class="flex flex-col gap-1">
                            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Last</span>
                            <h3 class="text-2xl font-display font-black tracking-tight">Session</h3>
                        </div>
                        {#if $lastCompletedSession}
                            <span class="text-base-content/30 text-xl">&rarr;</span>
                        {/if}
                    </div>

                    {#if $lastCompletedSession}
                        <div>
                            <p class="text-xl font-bold uppercase tracking-wide truncate">
                                {getSessionTitle($lastCompletedSession)}
                            </p>
                            <p class="text-sm font-mono uppercase tracking-widest text-base-content/50 mt-2">
                                {formatSessionDate($lastCompletedSession.date)}
                            </p>
                        </div>

                        <div class="mt-6 pt-4 border-t-2 border-base-content/10 grid grid-cols-3 gap-4">
                            <div class="flex flex-col gap-1">
                                <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Moves</span>
                                <span class="text-2xl font-display font-black">{getMovementCount($lastCompletedSession)}</span>
                            </div>
                            <div class="flex flex-col gap-1">
                                <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Sets</span>
                                <span class="text-2xl font-display font-black">{getTotalSets($lastCompletedSession)}</span>
                            </div>
                            <div class="flex flex-col gap-1">
                                <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Volume</span>
                                <span class="text-2xl font-display font-black">{formatVolume(getTotalVolume($lastCompletedSession))}</span>
                            </div>
                        </div>
                    {:else}
                        <div class="py-4">
                            <p class="text-base-content/50">No completed sessions</p>
                            <p class="text-xs text-base-content/30 mt-1">Complete your first session</p>
                        </div>
                    {/if}
                </button>

                <!-- Next Session Card -->
                <button
                    on:click={() => $nextSession && goToSession($nextSession)}
                    class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
                           cursor-pointer p-6 text-left w-full h-full
                           border-2 border-base-content/10 hover:border-base-content/30
                           {!$nextSession ? 'opacity-60 cursor-not-allowed' : ''}"
                    disabled={!$nextSession}
                >
                    <div class="flex items-start justify-between mb-6">
                        <div class="flex flex-col gap-1">
                            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Next</span>
                            <h3 class="text-2xl font-display font-black tracking-tight">Session</h3>
                        </div>
                        {#if $nextSession}
                            <span class="text-base-content/30 text-xl">&rarr;</span>
                        {/if}
                    </div>

                    {#if $nextSession}
                        <div>
                            <p class="text-xl font-bold uppercase tracking-wide truncate">
                                {getSessionTitle($nextSession)}
                            </p>
                            <p class="text-sm font-mono uppercase tracking-widest text-base-content/50 mt-2">
                                {formatSessionDate($nextSession.date)}
                            </p>
                        </div>

                        <div class="mt-6 pt-4 border-t-2 border-base-content/10">
                            <div class="flex items-baseline gap-2">
                                <span class="text-4xl font-display font-black">{getMovementCount($nextSession)}</span>
                                <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Movements Planned</span>
                            </div>
                        </div>
                    {:else}
                        <div class="py-4">
                            <p class="text-base-content/50">No upcoming session</p>
                            <p class="text-xs text-base-content/30 mt-1">Create a new session</p>
                        </div>
                    {/if}
                </button>

                <!-- Active Program Card -->
                <button
                    on:click={() => $activeProgram && goToProgram($activeProgram)}
                    class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
                           cursor-pointer p-6 text-left w-full h-full
                           border-2 border-base-content/10 hover:border-base-content/30
                           {!$activeProgram ? 'opacity-60 cursor-not-allowed' : ''}"
                    disabled={!$activeProgram}
                >
                    <div class="flex items-start justify-between mb-6">
                        <div class="flex flex-col gap-1">
                            <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Active</span>
                            <h3 class="text-2xl font-display font-black tracking-tight">Program</h3>
                        </div>
                        {#if $activeProgram}
                            <span class="text-base-content/30 text-xl">&rarr;</span>
                        {/if}
                    </div>

                    {#if $activeProgram}
                        {@const progress = getProgramProgress($activeProgram)}
                        <div>
                            <p class="text-xl font-bold uppercase tracking-wide truncate">
                                {$activeProgram.title || 'Untitled Program'}
                            </p>
                            <p class="text-sm font-mono uppercase tracking-widest text-base-content/50 mt-2">
                                {formatDate($activeProgram.startDate)} &ndash;
                                {formatDate($activeProgram.endDate)}
                            </p>
                        </div>

                        <div class="mt-6 pt-4 border-t-2 border-base-content/10">
                            <div class="flex items-center justify-between mb-2">
                                <span class="text-xs font-bold uppercase tracking-wider text-base-content/50">Progress</span>
                                <span class="text-sm font-mono">{progress.completed}/{progress.total}</span>
                            </div>
                            <progress
                                class="progress progress-primary w-full h-2"
                                value={progress.completed}
                                max={progress.total}
                            ></progress>
                        </div>
                    {:else}
                        <div class="py-4">
                            <p class="text-base-content/50">No active program</p>
                            <p class="text-xs text-base-content/30 mt-1">Create a training program</p>
                        </div>
                    {/if}
                </button>
            </div>

            <!-- Equipment Manager Link -->
            <div class="mb-8">
                <button
                    on:click={goToEquipmentManager}
                    class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
                           cursor-pointer p-6 text-left w-full
                           border-2 border-base-content/10 hover:border-base-content/30"
                >
                    <div class="flex items-center justify-between">
                        <div class="flex items-center gap-4">
                            <div class="w-12 h-12 rounded-xl bg-base-200 flex items-center justify-center">
                                <span class="text-2xl font-display font-black text-base-content/50">#</span>
                            </div>
                            <div>
                                <h3 class="text-lg font-bold">Equipment Manager</h3>
                                <p class="text-sm text-base-content/50">Manage movement bases and equipment</p>
                            </div>
                        </div>
                        <span class="text-base-content/30 text-xl">&rarr;</span>
                    </div>
                </button>
            </div>

            <!-- Recent Sessions -->
            <section>
                <div class="flex items-center justify-between mb-4">
                    <h2 class="text-2xl font-display font-black tracking-tight">Recent Sessions</h2>
                    <span class="text-sm font-mono uppercase tracking-widest text-base-content/50">
                        Last 2 weeks
                    </span>
                </div>

                {#if $recentSessions.length === 0}
                    <div class="card bg-base-100 shadow-editorial border-2 border-base-content/10 p-8 text-center">
                        <p class="text-base-content/50 mb-4">No recent sessions</p>
                        <button
                            class="btn btn-primary px-6 rounded-xl mx-auto"
                            on:click={openCreateSessionModal}
                        >
                            Create Your First Session
                        </button>
                    </div>
                {:else}
                    <div class="space-y-3">
                        {#each $recentSessions as session}
                            <button
                                on:click={() => goToSession(session)}
                                class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
                                       cursor-pointer p-4 text-left w-full
                                       border-2 border-base-content/10 hover:border-base-content/30"
                            >
                                <div class="flex items-center justify-between">
                                    <div class="flex items-center gap-4 flex-1 min-w-0">
                                        <div class="flex flex-col gap-1 min-w-0 flex-1">
                                            <div class="flex items-center gap-2">
                                                <span class="font-bold truncate">{getSessionTitle(session)}</span>
                                                <span class="badge {getStatusColor(session.status)} badge-sm shrink-0">
                                                    {getStatusText(session.status)}
                                                </span>
                                            </div>
                                            <span class="text-xs font-mono uppercase tracking-widest text-base-content/50">
                                                {formatSessionDate(session.date)}
                                            </span>
                                        </div>

                                        <div class="hidden sm:flex items-center gap-6 text-sm text-base-content/60">
                                            <div class="flex flex-col items-center">
                                                <span class="text-lg font-display font-black text-base-content">{getMovementCount(session)}</span>
                                                <span class="text-xs uppercase tracking-wider">moves</span>
                                            </div>
                                            {#if getTotalSets(session) > 0}
                                                <div class="flex flex-col items-center">
                                                    <span class="text-lg font-display font-black text-base-content">{getTotalSets(session)}</span>
                                                    <span class="text-xs uppercase tracking-wider">sets</span>
                                                </div>
                                            {/if}
                                        </div>
                                    </div>

                                    <span class="text-base-content/30 text-xl ml-4">&rarr;</span>
                                </div>
                            </button>
                        {/each}
                    </div>
                {/if}
            </section>

            <!-- All Programs -->
            {#if $programs.length > 0}
                <section class="mt-8">
                    <div class="flex items-center justify-between mb-4">
                        <h2 class="text-2xl font-display font-black tracking-tight">All Programs</h2>
                        <span class="text-sm font-mono uppercase tracking-widest text-base-content/50">
                            {$programs.length} total
                        </span>
                    </div>

                    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                        {#each $programs as program}
                            {@const progress = getProgramProgress(program)}
                            <button
                                on:click={() => goToProgram(program)}
                                class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
                                       cursor-pointer p-5 text-left w-full
                                       border-2 border-base-content/10 hover:border-base-content/30"
                            >
                                <div class="flex items-start justify-between mb-3">
                                    <div class="flex-1 min-w-0">
                                        <h3 class="font-bold truncate">{program.title || 'Untitled Program'}</h3>
                                        <p class="text-xs font-mono uppercase tracking-widest text-base-content/50 mt-1">
                                            {formatDate(program.startDate)} &ndash;
                                            {formatDate(program.endDate)}
                                        </p>
                                    </div>
                                    {#if program.isCompleted}
                                        <span class="badge badge-success badge-sm">Completed</span>
                                    {:else}
                                        <span class="badge badge-ghost badge-sm">Active</span>
                                    {/if}
                                </div>

                                <div class="flex items-center justify-between">
                                    <div class="flex items-center gap-2 flex-1">
                                        <progress
                                            class="progress progress-primary flex-1 h-2"
                                            value={progress.completed}
                                            max={progress.total}
                                        ></progress>
                                        <span class="text-xs font-mono text-base-content/50">
                                            {progress.completed}/{progress.total}
                                        </span>
                                    </div>
                                    <span class="text-base-content/30 text-lg ml-4">&rarr;</span>
                                </div>
                            </button>
                        {/each}
                    </div>
                </section>
            {/if}
        {/if}
    </div>
</div>

<!-- Create Program Modal -->
{#if createProgramModalOpen}
    <div class="modal modal-open">
        <div class="modal-box max-w-2xl rounded-xl border-2 border-base-content/10">
            <div class="flex justify-between items-center pb-4 border-b border-base-content/10">
                <h3 class="text-2xl font-display font-black">Create Program</h3>
                <button class="btn btn-sm btn-ghost btn-square rounded-xl" on:click={closeCreateProgramModal}>
                    <span class="text-lg">&times;</span>
                </button>
            </div>

            <div class="py-6 space-y-4">
                <div class="form-control w-full">
                    <label class="label" for="create-program-title">
                        <span class="label-text font-bold uppercase text-xs tracking-wider">Title</span>
                    </label>
                    <input
                        id="create-program-title"
                        type="text"
                        placeholder="e.g., Strength Block 1"
                        class="input input-bordered w-full rounded-xl"
                        bind:value={programForm.title}
                    />
                </div>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div class="form-control w-full">
                        <label class="label" for="create-program-start">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Start Date</span>
                        </label>
                        <input
                            id="create-program-start"
                            type="date"
                            class="input input-bordered w-full rounded-xl"
                            bind:value={programForm.startDate}
                        />
                    </div>

                    <div class="form-control w-full">
                        <label class="label" for="create-program-end">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">End Date</span>
                        </label>
                        <input
                            id="create-program-end"
                            type="date"
                            class="input input-bordered w-full rounded-xl"
                            bind:value={programForm.endDate}
                        />
                    </div>
                </div>

                <div class="form-control w-full">
                    <label class="label" for="create-program-tags">
                        <span class="label-text font-bold uppercase text-xs tracking-wider">Tags</span>
                    </label>
                    <input
                        id="create-program-tags"
                        type="text"
                        placeholder="e.g., strength, hypertrophy (comma separated)"
                        class="input input-bordered w-full rounded-xl"
                        bind:value={programForm.tagsCsv}
                    />
                </div>
            </div>

            <div class="flex gap-4 pt-4 border-t border-base-content/10">
                <button class="btn btn-ghost px-5 rounded-xl flex-1" on:click={closeCreateProgramModal}>
                    Cancel
                </button>
                <button
                    class="btn btn-primary px-5 rounded-xl flex-1"
                    disabled={!programForm.title || !programForm.startDate || !programForm.endDate}
                >
                    Create Program
                </button>
            </div>
        </div>
        <!-- svelte-ignore a11y-click-events-have-key-events a11y-no-static-element-interactions -->
        <div class="modal-backdrop bg-base-300/80" on:click={closeCreateProgramModal}></div>
    </div>
{/if}

<!-- Create Session Modal -->
{#if createSessionModalOpen}
    <div class="modal modal-open">
        <div class="modal-box max-w-2xl rounded-xl border-2 border-base-content/10">
            <div class="flex justify-between items-center pb-4 border-b border-base-content/10">
                <h3 class="text-2xl font-display font-black">Create Session</h3>
                <button class="btn btn-sm btn-ghost btn-square rounded-xl" on:click={closeCreateSessionModal}>
                    <span class="text-lg">&times;</span>
                </button>
            </div>

            <div class="py-6 space-y-4">
                <div class="form-control w-full">
                    <label class="label" for="create-session-date">
                        <span class="label-text font-bold uppercase text-xs tracking-wider">Date</span>
                    </label>
                    <input
                        id="create-session-date"
                        type="date"
                        class="input input-bordered w-full rounded-xl"
                        bind:value={sessionForm.date}
                    />
                </div>

                <div class="form-control w-full">
                    <label class="label" for="create-session-notes">
                        <span class="label-text font-bold uppercase text-xs tracking-wider">Notes / Title</span>
                    </label>
                    <textarea
                        id="create-session-notes"
                        placeholder="e.g., Upper Body Push, Leg Day"
                        class="textarea textarea-bordered w-full rounded-xl"
                        rows="3"
                        bind:value={sessionForm.notes}
                    ></textarea>
                </div>

                {#if $programs.length > 0}
                    <div class="form-control w-full">
                        <label class="label" for="create-session-program">
                            <span class="label-text font-bold uppercase text-xs tracking-wider">Program (Optional)</span>
                        </label>
                        <select id="create-session-program" class="select select-bordered w-full rounded-xl">
                            <option value="">No program (standalone session)</option>
                            {#each $programs as program}
                                <option value={program.trainingProgramID}>{program.title || 'Untitled'}</option>
                            {/each}
                        </select>
                    </div>
                {/if}
            </div>

            <div class="flex gap-4 pt-4 border-t border-base-content/10">
                <button class="btn btn-ghost px-5 rounded-xl flex-1" on:click={closeCreateSessionModal}>
                    Cancel
                </button>
                <button
                    class="btn btn-primary px-5 rounded-xl flex-1"
                    disabled={!sessionForm.date}
                >
                    Create Session
                </button>
            </div>
        </div>
        <!-- svelte-ignore a11y-click-events-have-key-events a11y-no-static-element-interactions -->
        <div class="modal-backdrop bg-base-300/80" on:click={closeCreateSessionModal}></div>
    </div>
{/if}
