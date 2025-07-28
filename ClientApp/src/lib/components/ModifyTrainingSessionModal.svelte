<script lang="ts">
    import {
        GetTrainingSessionRequest,
        ModifyTrainingSessionEndpointClient,
        TrainingProgramDTO,
        TrainingSessionDTO,
    } from "$lib/api/ApiClient";
    import { createEventDispatcher } from "svelte";
    import { onMount } from "svelte";
    import PlannedSessionViewer from "./PlannedSessionViewer.svelte";
    import AiModifiedSessionViewer from "./AIModifiedSessionViewer.svelte";
    export let loadSession: () => Promise<void>;
    const baseUrl =
        typeof window !== "undefined"
            ? window.location.origin
            : "http://localhost:5174";
    export let show = false;
    export let session: TrainingSessionDTO;
    const dispatch = createEventDispatcher();
    let isLoading = true;
    let startedModifying = false;
    let modifiedSession: TrainingSessionDTO;

    onMount(async () => {
        show = true;
    });

    function close() {
        loadSession();
        show = false;
        dispatch("close");
    }

    async function ModifySession() {
        startedModifying = true;
        const client = new ModifyTrainingSessionEndpointClient(baseUrl);
        try {
            const request = GetTrainingSessionRequest.fromJS({
                trainingSessionID: session.trainingSessionID,
                programID: session.trainingProgramID,
            });
            modifiedSession = await client.modifyTrainingSession(request);
            isLoading = false;
        } catch (error) {
            console.error("Failed to modify session:", error);
        }
    }
</script>

{#if show}
    <div
        class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 w-full"
    >
        <div class="bg-white rounded-lg shadow-lg p-5 w-3/4">
            <p class="text-2xl pb-2 font-bold">Modify Training Session</p>
            <div class="badge badge-info p-1 m-0">AI</div>
            <div class="divider m-0 mb-5 p-0"></div>
            <div class="flex flex-col md:flex-row gap-4 justify-between">
                <!-- Current Session -->
                <div class="flex flex-col">
                    <h2 class="text-lg font-bold mb-2">Existing Session</h2>
                    <PlannedSessionViewer
                        {session}
                        slug={session.trainingProgramID}
                        loadSessions={loadSession}
                        on:close={close}
                    />
                </div>

                <!-- AI Modifying Section -->
                {#if isLoading && startedModifying}
                    <div class="flex flex-col justify-center text-center w-64">
                        <div>
                            <span class="loading loading-spinner loading-lg"
                            ></span>
                            <p class="mt-2 text-center">
                                AI is modifying program...
                            </p>
                        </div>
                    </div>
                          
                {:else}
                    <div class="flex flex-col justify-center text-left w-64">
                        <p class="text-sm">
                            Lionintelligence will modify this training session
                            based on wearables data, wellness scores, training
                            performance, and recent activities.
                        </p>
                    </div>
                {/if}
                 <!-- Modified Session -->
                    <div class="flex flex-col w-64">
                        <h2 class="text-lg font-bold mb-2">Modified Session</h2>
                        <AiModifiedSessionViewer
                            {session}
                            slug={session.trainingProgramID}
                            loadSessions={loadSession}
                            on:close={close}
                        />
                    </div>
            </div>
            <div class="divider m-0 mt-5 p-0"></div>
            <!-- Action Buttons -->
            <div class="mt-5 flex justify-end gap-2">
                <button class="btn btn-success" on:click={ModifySession}
                    >Modify Session</button
                >
                <button class="btn btn-error" on:click={close}>Exit</button>
            </div>
        </div>
    </div>
{/if}
