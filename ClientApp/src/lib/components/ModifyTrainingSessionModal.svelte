<script lang="ts">
    import {
        ModifyTrainingSessionWithAIRequest,
        ModifyTrainingSessionEndpointClient,
        TrainingProgramDTO,
        TrainingSessionDTO,
    } from "$lib/api/ApiClient";
    import { createEventDispatcher } from "svelte";
    import { onMount } from "svelte";
    import PlannedSessionViewer from "./PlannedSessionViewer.svelte";
    import AiModifiedSessionViewer from "./AIModifiedSessionViewer.svelte";
    import { page } from "$app/stores";
    export let loadSession: () => Promise<void>;
    const baseUrl =
        typeof window !== "undefined"
            ? window.location.origin
            : "http://localhost:5174";
    export let show = false;
    export let session: TrainingSessionDTO;
    const dispatch = createEventDispatcher();
    let modifyingSessionRunning = false;
    let modifiedSession: TrainingSessionDTO;
    let userPrompt: string = "";

    onMount(async () => {
        show = true;
    });

    function close() {
        loadSession();
        show = false;
        dispatch("close");
    }

    async function ModifySession() {
        modifyingSessionRunning = true;
        const client = new ModifyTrainingSessionEndpointClient(baseUrl);
        try {
            const request = ModifyTrainingSessionWithAIRequest.fromJS({
                trainingSessionID: session.trainingSessionID,
                trainingProgramID: session.trainingProgramID,
                userPrompt: userPrompt,
            });
            console.log("Requesting AI modification for session:", request);
            modifiedSession = await client.modifyTrainingSession(request);
            modifyingSessionRunning = false;
        } catch (error) {
            alert("Failed to modify session.");
            modifyingSessionRunning = false;
            console.error("Failed to modify session:", error);
        }
    }
</script>

{#if show}
    <div
        class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 w-full"
    >
        <div
            class="bg-white rounded-lg shadow-2xl p-8 w-[90vw] max-w-[1600px] min-h-[80vh] max-h-screen overflow-y-auto flex flex-col gap-6"
        >
            <!-- Title Row -->
            <div
                class="flex flex-col md:flex-row items-center justify-between gap-4 w-full text-center md:text-left"
            >
                <div class="flex items-center gap-4">
                    <p class="text-6xl font-bold">Modify Training Session</p>
                </div>
                <div class="flex gap-2 md:mt-0">
                    <button class="btn btn-success" on:click={ModifySession}
                        >Modify Session</button
                    >
                    <button class="btn btn-error" on:click={close}>Exit</button>
                </div>
            </div>

            <div class="divider m-0"></div>
            <fieldset class="fieldset w-full">
                <legend class="fieldset-legend font-bold mb-2"
                    >Additional Modification Instructions</legend
                >
                <textarea
                    class="textarea h-24 w-full"
                    placeholder="..."
                    bind:value={userPrompt}
                ></textarea>
            </fieldset>
            <!-- Sessions Row -->
            <div
                class="flex flex-col md:flex-row gap-8  w-full justify-between items-start"
            >
                <!-- Current Session -->
                <div class="flex-1 min-w-[320px] w-full">
                    <h2 class="text-2xl font-bold">Existing Session</h2>
                    <div class="divider m-0 p-0"></div>
                    <div class="mb-4">
                        <h4 class="text-md font-semibold mb-1">
                            Session Notes
                        </h4>
                        <div
                            class="border rounded bg-gray-50 p-2 h-32 max-h-32 overflow-y-auto text-sm"
                        >
                            {session.notes}
                        </div>
                    </div>
                    {#if session && session.movements && session.movements.length > 0}
                        {#each session.movements as movement}
                            <div class="mb-6">
                                <div class="flex flex-row gap-2 items-center">
                                    <h3 class="text-lg font-semibold mb-1">
                                        {movement.movementModifier.name}
                                        {movement.movementBase.name}
                                    </h3>
                                    <div class="badge badge-xs p-2">
                                        {movement.movementModifier.equipment
                                            .name}
                                    </div>
                                </div>
                                <table
                                    class="table w-full border rounded-lg overflow-hidden"
                                >
                                    <thead class="bg-gray-100">
                                        <tr>
                                            <th class="px-2 py-1">Set</th>
                                            <th class="px-2 py-1">Weight</th>
                                            <th class="px-2 py-1">Reps</th>
                                            <th class="px-2 py-1">RPE</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {#each movement.sets as set, i}
                                            <tr class="border-t">
                                                <td class="px-2 py-1"
                                                    >{i + 1}</td
                                                >
                                                <td class="px-2 py-1"
                                                    >{set.recommendedWeight}</td
                                                >
                                                <td class="px-2 py-1"
                                                    >{set.recommendedReps}</td
                                                >
                                                <td class="px-2 py-1"
                                                    >{set.recommendedRPE}</td
                                                >
                                            </tr>
                                        {/each}
                                    </tbody>
                                </table>
                            </div>
                        {/each}
                    {:else}
                        <p class="text-sm">No movements found.</p>
                    {/if}
                </div>

                <!-- Modified Session -->
                <div class="flex-1 min-w-[320px]">
                    <h2 class="text-2xl font-bold ">Modified Session</h2>
                    <div class="divider m-0 p-0"></div>
                    {#if modifyingSessionRunning}
                        <div
                            class="flex flex-col justify-center items-center h-full"
                        >
                            <span class="loading loading-spinner loading-lg"
                            ></span>
                            <p class="mt-2 text-center">
                                AI is modifying program...
                            </p>
                        </div>
                    {:else if modifiedSession && modifiedSession.movements && modifiedSession.movements.length > 0}
                        <div class="mb-4">
                            <h4 class="text-md font-semibold mb-1">
                                Session Notes
                            </h4>
                            <div
                                class="border rounded bg-gray-50 p-2 h-32 max-h-32 overflow-y-auto text-sm"
                            >
                                {modifiedSession.notes}
                            </div>
                        </div>
                        {#each modifiedSession.movements as movement, mIdx}
                            <div class="mb-6">
                                <div class="flex flex-row gap-2 items-center">
                                    <h3 class="text-lg font-semibold mb-1">
                                        {movement.movementModifier.name}
                                        {movement.movementBase.name}
                                    </h3>
                                    <div class="badge badge-xs p-2">
                                        {movement.movementModifier.equipment?.name}
                                    </div>
                                </div>
                                <table class="table w-full border rounded-lg overflow-hidden">
                                    <thead class="bg-gray-100">
                                        <tr>
                                            <th class="px-2 py-1">Set</th>
                                            <th class="px-2 py-1">Weight</th>
                                            <th class="px-2 py-1">Reps</th>
                                            <th class="px-2 py-1">RPE</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {#each movement.sets as set, i}
                                            {#if session && session.movements}
                                                {#if mIdx < session.movements.length}
                                                    {#if i < session.movements[mIdx].sets.length}
                                                        {@const origSet = session.movements[mIdx].sets[i]}
                                                        {@const changed = set.recommendedWeight !== origSet.recommendedWeight || set.recommendedReps !== origSet.recommendedReps || set.recommendedRPE !== origSet.recommendedRPE}
                                                        <tr class="border-t {changed ? 'bg-info/50' : ''}">
                                                            <td class="px-2 py-1">{i + 1}</td>
                                                            <td class="px-2 py-1">{set.recommendedWeight}</td>
                                                            <td class="px-2 py-1">{set.recommendedReps}</td>
                                                            <td class="px-2 py-1">{set.recommendedRPE}</td>
                                                        </tr>
                                                    {:else}
                                                        <!-- New set added -->
                                                        <tr class="border-t bg-info/60">
                                                            <td class="px-2 py-1">{i + 1}</td>
                                                            <td class="px-2 py-1">{set.recommendedWeight}</td>
                                                            <td class="px-2 py-1">{set.recommendedReps}</td>
                                                            <td class="px-2 py-1">{set.recommendedRPE}</td>
                                                        </tr>
                                                    {/if}
                                                {:else}
                                                    <!-- New movement added -->
                                                    <tr class="border-t bg-info/60">
                                                        <td class="px-2 py-1">{i + 1}</td>
                                                        <td class="px-2 py-1">{set.recommendedWeight}</td>
                                                        <td class="px-2 py-1">{set.recommendedReps}</td>
                                                        <td class="px-2 py-1">{set.recommendedRPE}</td>
                                                    </tr>
                                                {/if}
                                            {:else}
                                                <tr class="border-t">
                                                    <td class="px-2 py-1">{i + 1}</td>
                                                    <td class="px-2 py-1">{set.recommendedWeight}</td>
                                                    <td class="px-2 py-1">{set.recommendedReps}</td>
                                                    <td class="px-2 py-1">{set.recommendedRPE}</td>
                                                </tr>
                                            {/if}
                                        {/each}
                                    </tbody>
                                </table>
                            </div>
                        {/each}
                    {:else}
                        <p class="text-sm">Not yet generated.</p>
                    {/if}
                </div>
            </div>
        </div>
    </div>
{/if}
