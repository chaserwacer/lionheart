<script lang="ts">
    import {
        fetchTodaysWellnessState,
        todaysWellnessState,
        pageUpdate,
    } from "$lib/stores";
    import { writable } from "svelte/store";
    /**
     * @type {typeof import("svelte-chartjs").Line}
     */
    let myLine: typeof import("svelte-chartjs").Line;
    let showModal = writable(false);
    let energyInput = 1;
    let motivationInput = 1;
    let moodInput = 1;
    let stressInput = 1;
    export let selectedDate: string;

    function openModal() {
        showModal.set(true);
    }

    function closeModal() {
        showModal.set(false);
    }

    async function trackWellnessState() {
        try {
            const response = await self.fetch("/api/wellness/add", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify({
                    Date: selectedDate,
                    energy: energyInput,
                    motivation: motivationInput,
                    mood: moodInput,
                    stress: stressInput,
                }),
            });

            if (response.ok) {
                closeModal();
                fetchTodaysWellnessState(fetch);
                $pageUpdate = new Date();
                energyInput = 1;
                motivationInput = 1;
                moodInput = 1;
                stressInput = 1;
            } else {
                console.error(
                    "Failed to track Wellness State:",
                    response.statusText,
                );
            }
        } catch (error) {
            console.error("Error tracking Wellness State", error);
        }
    }
</script>

<button class="btn btn-sm btn-outline btn-primary" on:click={openModal}
    >Track Wellness</button
>

{#if $showModal}
    <div class=" flex items-center justify-center">
        <div class="modal modal-open">
            <div class="modal-box">
                <h2 class="font-bold text-lg">Daily Wellness Tracker</h2>
                <p>
                    Please fill out these values during the first quarter of
                    your day.
                </p>

                <form class="space-y-4 mt-4">
                    <div>
                        <label
                            >Energy
                            <input
                                type="range"
                                min="1"
                                max="5"
                                class="range"
                                step="1"
                                bind:value={energyInput}
                            />
                            <div
                                class="flex w-full justify-between px-2 text-xs"
                            >
                                <span>nonexistent</span>
                                <span>low</span>
                                <span>normal</span>
                                <span>high</span>
                                <span>extreme</span>
                            </div>
                        </label>
                    </div>

                    <div>
                        <label
                            >Motivation
                            <input
                                type="range"
                                min="1"
                                max="5"
                                class="range"
                                step="1"
                                bind:value={motivationInput}
                            />
                            <div
                                class="flex w-full justify-between px-2 text-xs"
                            >
                                <span>nonexistent</span>
                                <span>low</span>
                                <span>normal</span>
                                <span>high</span>
                                <span>extreme</span>
                            </div>
                        </label>
                    </div>

                    <div>
                        <label
                            >Mood
                            <input
                                type="range"
                                min="1"
                                max="5"
                                class="range"
                                step="1"
                                bind:value={moodInput}
                            />
                            <div
                                class="flex w-full justify-between px-2 text-xs"
                            >
                                <span>terrible</span>
                                <span>bad</span>
                                <span>okay</span>
                                <span>good</span>
                                <span>great</span>
                            </div>
                        </label>
                    </div>

                    <div>
                        <label
                            >Stress
                            <input
                                type="range"
                                min="1"
                                max="5"
                                class="range"
                                step="1"
                                bind:value={stressInput}
                            />
                            <div
                                class="flex w-full justify-between px-2 text-xs"
                            >
                                <span>nonexistent</span>
                                <span>low</span>
                                <span>normal</span>
                                <span>high</span>
                                <span>extreme</span>
                            </div>
                        </label>
                    </div>

                    <div class="modal-action">
                        <button
                            type="button"
                            class="btn btn-neutral"
                            on:click={trackWellnessState}>Track Scores</button
                        >
                        <button type="button" class="btn" on:click={closeModal}
                            >Cancel</button
                        >
                    </div>
                </form>
            </div>
        </div>
    </div>
{/if}
