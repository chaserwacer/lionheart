<script lang="ts">
    import { slugify } from "$lib/utils/slugify";
    import { goto } from "$app/navigation";
    import PlannedSessionViewer from "$lib/components/PlannedSessionViewer.svelte";
    import CompleteSessionViewer from "$lib/components/CompletedSessionViewer.svelte";
    import {
        TrainingProgramDTO,
        TrainingSessionDTO,
        TrainingSessionStatus,
        DeleteTrainingProgramEndpointClient,
    } from "$lib/api/ApiClient";

    export let program: TrainingProgramDTO;
    export let loadPrograms: () => Promise<void>;

    // Local baseUrl for API calls
    const baseUrl =
        typeof window !== "undefined"
            ? window.location.origin
            : "http://localhost:5174";

    function goToProgram(program: TrainingProgramDTO) {
        goto(`/programs/${slugify(program.trainingProgramID)}`);
    }

    function formatDate(dateInput: Date | string | undefined): string {
        if (!dateInput) return "—";
        const dateStr =
            typeof dateInput === "string"
                ? dateInput.slice(0, 10)
                : dateInput.toISOString().slice(0, 10);
        const date = new Date(dateStr + "T00:00:00");
        const options = { month: "short", day: "numeric" } as const;
        return date.toLocaleDateString(undefined, options);
    }

    function getTypeColor(type: string) {
        switch (type) {
            case "Powerlifting":
                return "bg-error text-error-content";
            case "Bodybuilding":
                return "bg-secondary text-secondary-content";
            case "General Fitness":
                return "bg-primary text-primary-content";
            case "Running":
                return "bg-success text-success-content";
            case "Biking":
                return "bg-warning text-warning-content";
            case "Swimming":
                return "bg-info text-info-content";
            default:
                return "bg-neutral text-neutral-content";
        }
    }

    function getRelevantSessions(
        program: TrainingProgramDTO,
    ): [TrainingSessionDTO?, TrainingSessionDTO?] {
        const today = new Date();
        today.setHours(0, 0, 0, 0);
        // Most recent completed session before today
        const completedSessions = program.trainingSessions
            .filter(
                (session) =>
                    session.status === TrainingSessionStatus._2 &&
                    new Date(session.date).setHours(0, 0, 0, 0) <=
                        today.getTime(),
            )
            .sort(
                (a, b) =>
                    new Date(b.date).getTime() - new Date(a.date).getTime(),
            );
        // Next planned session today or later
        const plannedSessions = program.trainingSessions
            .filter(
                (session) =>
                    session.status === TrainingSessionStatus._0 &&
                    new Date(session.date) >= today,
            )
            .sort(
                (a, b) =>
                    new Date(a.date).getTime() - new Date(b.date).getTime(),
            );
        return [completedSessions[0], plannedSessions[0]];
    }

    function calculateProgress(program: TrainingProgramDTO): number {
        const sessions = program.trainingSessions;
        const completed = sessions.filter(
            (s) => s.status === TrainingSessionStatus._2,
        ).length;
        return sessions.length === 0
            ? 0
            : Math.round((completed / sessions.length) * 100);
    }

    async function deleteProgram(programID: string) {
        const confirmed = confirm(
            "Are you sure you want to delete this program?",
        );
        if (!confirmed) return;
        try {
            const deleteProgramClient = new DeleteTrainingProgramEndpointClient(
                baseUrl,
            );
            await deleteProgramClient.delete4(programID);
            await loadPrograms();
        } catch (err) {
            alert("Failed to delete program.");
            console.error(err);
        }
    }

    // Prepare display sessions for this program only
    let displaySessions: [TrainingSessionDTO?, TrainingSessionDTO?] = [];
    $: displaySessions = getRelevantSessions(program);
</script>

<div class="relative group w-full">
    <div
        role="link"
        tabindex="0"
        class="card bg-base-100 text-base-content border border-base-300 shadow-md hover:shadow-lg transition cursor-pointer w-full min-h-[400px] max-w-full overflow-hidden"
        on:click={() => goToProgram(program)}
        on:keydown={(e) => e.key === "Enter" && goToProgram(program)}
    >
        <div class="card-body flex flex-col justify-between w-full">
            <!-- Header -->
            <div class="flex flex-col gap-2 mb-2">
                <div class="flex items-center justify-between">
                    <h2
                        class="card-title text-2xl md:text-4xl pb-3 truncate w-full"
                    >
                        {program.title}
                    </h2>
                    <button
                        type="button"
                        on:click|stopPropagation={() =>
                            program.trainingProgramID &&
                            deleteProgram(program.trainingProgramID)}
                        class="text-error hover:text-error-content text-xl font-bold ml-2"
                        title="Delete program"
                    >
                        &times;
                    </button>
                </div>
                <div class="divider m-0 p-0"></div>
                <div class="flex flex-col md:flex-row gap-6 mt-2 w-full justify-between">
                    <!-- Left: Dates stacked -->
                    <div class="flex flex-col gap-2 text-center md:text-left">
                        <h1
                            class="text-xl badge badge-primary font-extrabold  p-3 text-center w-full"
                        >
                            {program.isCompleted ? "COMPLETE" : "ACTIVE"}
                        </h1>
                        <div class="flex flex-row gap-2 ">
                            <span
                                class={`text-xs w-full text-center font-semibold px-3 py-1 rounded-full ${getTypeColor(program.tags?.[0] ?? "")}`}
                            >
                                {program.tags?.[0] ?? "Unknown"}
                            </span>
                        </div>
                        <div class="stat bg-base-200 rounded-lg px-4 py-2">
                            <div class="stat-title text-xs">Start</div>
                            <div class="stat-value text-base">
                                {formatDate(program.startDate)}
                            </div>
                        </div>
                        <div class="stat bg-base-200 rounded-lg px-4 py-2">
                            <div class="stat-title text-xs">End</div>
                            <div class="stat-value text-base">
                                {formatDate(program.endDate)}
                            </div>
                        </div>
                        
                    </div>
                    <!-- Right: Completed and Next planned session -->
                    <div
                        class=" flex flex-row justify-center items-center"
                    >
                        {#if displaySessions[0] || displaySessions[1]}
                            <div
                                class="flex flex-col md:flex-row gap-4 w-full bg-transparent items-center"
                            >
                                {#if displaySessions[0]}
                                    <div class="px-0 py-0">
                                        <div class="font-semibold text-xs mb-1">
                                            Last Completed
                                        </div>
                                        <CompleteSessionViewer
                                            slug={program.trainingProgramID}
                                            session={displaySessions[0]}
                                            loadSessions={loadPrograms}
                                        />
                                    </div>
                                {/if}
                                {#if displaySessions[1]}
                                    <div class="px-0 py-0">
                                        <div class="font-semibold text-xs mb-1">
                                            Next Planned
                                        </div>
                                        <PlannedSessionViewer
                                            slug={program.trainingProgramID}
                                            session={displaySessions[1]}
                                            loadSessions={loadPrograms}
                                        />
                                    </div>
                                {/if}
                            </div>
                        {/if}
                    </div>
                </div>
            </div>
            <!-- Progress -->
            <div class="mt-6">
                <progress
                    class="progress w-full progress-primary"
                    value={calculateProgress(program)}
                    max="100"
                ></progress>
                <p class="text-sm text-right mt-1">
                    {calculateProgress(program)}%
                </p>
            </div>
        </div>
    </div>
</div>
