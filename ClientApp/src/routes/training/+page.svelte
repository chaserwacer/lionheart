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
      import CreateTrainingSessionModal from "$lib/components/modals/CreateTrainingSessionModal.svelte";
    import CreateTrainingProgramModal from "$lib/components/modals/CreateTrainingProgramModal.svelte";



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



    function openProgramModal() {
    (document.getElementById("create_program_modal") as HTMLDialogElement)?.showModal();
    }

    function openSessionModal() {
    (document.getElementById("create_session_modal") as HTMLDialogElement)?.showModal();
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
                        on:click={openSessionModal}
                    >
                        New Session
                    </button>
                    <button
                        class="btn btn-outline px-5 rounded-xl"
                        on:click={openProgramModal}
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
                                {new Date($activeProgram.startDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })} &ndash;
                                {new Date($activeProgram.endDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })}
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
                            on:click={openSessionModal}
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
                                            {new Date(program.startDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })} &ndash;
                                            {new Date(program.endDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })}
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


<CreateTrainingProgramModal modalId="create_program_modal" />

<CreateTrainingSessionModal modalId="create_session_modal" />
