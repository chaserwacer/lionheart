<script>
    import { onMount } from "svelte";
    import {
        fetchLastWeekActivityMinutes,
        fetchTodaysActivities,
    } from "./activityStore";

    let dateTime = "";
    let hours = 0;
    let minutes = 0;
    let caloriesBurned = 0;
    let name = "";
    let userSummary = "";
    let accumulatedFatigue = 3;
    let difficultyRating = 3;
    let engagementRating = 3;
    let externalVariablesRating = 3;

    let isBiking = false;
    let isRunWalk = false;
    let isLift = false;
    let isNoSpecialType = true;

    let tonnage = 0;
    let liftType = "";
    let liftFocus = "";

    let distance = 0.0;
    let elevationGain = 0;
    let averagePower = 0;
    let averageSpeed = 0;
    let rideType = "";

    let runType = "";
    let paceMinutes = 0;
    let paceSeconds = 0;

    import { createEventDispatcher } from "svelte";

    const dispatch = createEventDispatcher();

    function performUpdates() {
        // Perform necessary updates here

        // Call the updatePageInfo function in the parent
        dispatch("update");
    }

    // @ts-ignore
    function handleActivityChange(event) {
        const value = event.target.value;
        isBiking = value === "biking";
        isRunWalk = value === "runwalk";
        isLift = value === "lift";
        isNoSpecialType = value === "nospecialtype";
    }

    let showModal = false;

    function openModal() {
        showModal = true;
    }

    function closeModal() {
        showModal = false;
    }

    async function trackActivity() {
        const TimeSpan = hours * 60 + minutes;
        if (isBiking) {
            try {
                const response = await self.fetch(
                    "/api/activity/addrideactivity",
                    {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                        },
                        body: JSON.stringify({
                            dateTime: dateTime,
                            timeInMinutes: TimeSpan,
                            caloriesBurned: caloriesBurned,
                            name: name,
                            userSummary: userSummary,
                            accumulatedFatigue: accumulatedFatigue,
                            difficultyRating: difficultyRating,
                            engagementRating: engagementRating,
                            externalVariablesRating: externalVariablesRating,
                            distance: distance,
                            elevationGain: elevationGain,
                            averagePower: averagePower,
                            averageSpeed: averageSpeed,
                            rideType: rideType,
                        }),
                    },
                );

                if (response.ok) {
                    fetchTodaysActivities(fetch);
                    fetchLastWeekActivityMinutes(fetch);
                    closeModal();
                    performUpdates();
                } else {
                    console.error(
                        "Failed to track activity:",
                        response.statusText,
                    );
                }
            } catch (error) {
                console.error("Error tracking activity", error);
            }
        } else if (isLift) {
            try {
                const response = await self.fetch(
                    "/api/activity/addliftactivity",
                    {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                        },
                        body: JSON.stringify({
                            dateTime: dateTime,
                            timeInMinutes: TimeSpan,
                            caloriesBurned: caloriesBurned,
                            name: name,
                            userSummary: userSummary,
                            accumulatedFatigue: accumulatedFatigue,
                            difficultyRating: difficultyRating,
                            engagementRating: engagementRating,
                            externalVariablesRating: externalVariablesRating,
                            tonnage: tonnage,
                            liftType: liftType,
                            liftFocus: liftFocus,
                        }),
                    },
                );

                if (response.ok) {
                    fetchTodaysActivities(fetch);
                    fetchLastWeekActivityMinutes(fetch);
                    closeModal();
                } else {
                    console.error(
                        "Failed to track activity:",
                        response.statusText,
                    );
                }
            } catch (error) {
                console.error("Error tracking activity", error);
            }
        } else if (isRunWalk) {
            let averagePace = paceMinutes * 60 + paceSeconds;
            try {
                const response = await self.fetch(
                    "/api/activity/addrunwalkactivity",
                    {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                        },
                        body: JSON.stringify({
                            dateTime: dateTime,
                            timeInMinutes: TimeSpan,
                            caloriesBurned: caloriesBurned,
                            name: name,
                            userSummary: userSummary,
                            accumulatedFatigue: accumulatedFatigue,
                            difficultyRating: difficultyRating,
                            engagementRating: engagementRating,
                            externalVariablesRating: externalVariablesRating,
                            distance: distance,
                            elevationGain: elevationGain,
                            averagePace: averagePace,
                            mileSplitsInSeconds: [0],
                            runType: runType,
                        }),
                    },
                );

                if (response.ok) {
                    fetchTodaysActivities(fetch);
                    fetchLastWeekActivityMinutes(fetch);
                    closeModal();
                } else {
                    console.error(
                        "Failed to track activity:",
                        response.statusText,
                    );
                }
            } catch (error) {
                console.error("Error tracking activity", error);
            }
        } else {
            try {
                const response = await self.fetch("/api/activity/addactivity", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                    },
                    body: JSON.stringify({
                        dateTime: dateTime,
                        timeInMinutes: TimeSpan,
                        caloriesBurned: caloriesBurned,
                        name: name,
                        userSummary: userSummary,
                        accumulatedFatigue: accumulatedFatigue,
                        difficultyRating: difficultyRating,
                        engagementRating: engagementRating,
                        externalVariablesRating: externalVariablesRating,
                    }),
                });

                if (response.ok) {
                    fetchTodaysActivities(fetch);
                    fetchLastWeekActivityMinutes(fetch);
                    closeModal();
                } else {
                    console.error(
                        "Failed to track activity:",
                        response.statusText,
                    );
                }
            } catch (error) {
                console.error("Error tracking activity", error);
            }
        }

        closeModal();
    }
</script>

<button class="btn border border-primary" on:click={openModal}
    >Track Activity</button
>

{#if showModal}
    <div class="modal modal-open">
        <div class="modal-box text-center">
            <h2 class="font-bold text-xl">Activity Tracker</h2>
            <!-- <
            <p class="w-1/2">
                Please fill out the info below regarding your completed activity
            </p> -->
            <select
                class="select select-bordered mt-2 w-25"
                on:change={handleActivityChange}
            >
                <option value="nospecialtype" selected={isNoSpecialType}
                    >No Special Type</option
                >
                <option value="biking" selected={isBiking}>Biking</option>
                <option value="runwalk" selected={isRunWalk}>Run / Walk</option>
                <option value="lift" selected={isLift}>Lift</option>
            </select>

            <div class="divider font-bold">Base Data</div>
            <form on:submit|preventDefault={trackActivity}>
                <div class="form-control">
                    <div class="form-control">
                        <label class="label">
                            <span class="label-text">Session Name</span>
                            <input
                                type="text"
                                bind:value={name}
                                class="input input-bordered"
                            />
                        </label>
                    </div>
                    <label class="label">
                        <span class="label-text">DateTime</span>
                        <input
                            type="datetime-local"
                            bind:value={dateTime}
                            class="input input-bordered"
                        />
                    </label>
                </div>

                <div class="form-control">
                    <div class="label">
                        <span class="label-text">Length (hrs, mins)</span>

                        <div class="flex space-x-2">
                            <input
                                type="number"
                                min="0"
                                bind:value={hours}
                                class="input input-bordered w-20 mr-2"
                                placeholder="Hours"
                            />
                            :
                            <input
                                type="number"
                                min="0"
                                max="59"
                                bind:value={minutes}
                                class="input input-bordered w-20"
                                placeholder="Minutes"
                            />
                        </div>
                    </div>
                </div>

                <div class="form-control">
                    <label class="label">
                        <span class="label-text">Calories Burned</span>
                        <input
                            type="number"
                            min="0"
                            bind:value={caloriesBurned}
                            class="input input-bordered"
                        />
                    </label>
                </div>

                <div class="divider font-bold">Sport Specific Data</div>

                {#if isBiking}
                    <div class="form-control">
                        <label class="label label-primary">
                            <span class="label-text">Distance</span>
                            <input
                                type="number"
                                min="0"
                                step="0.01"
                                bind:value={distance}
                                class="input input-bordered"
                            />
                        </label>
                    </div>
                    <div class="form-control">
                        <label class="label label-primary">
                            <span class="label-text">Elevation Gain</span>
                            <input
                                type="number"
                                min="0"
                                bind:value={elevationGain}
                                class="input input-bordered"
                            />
                        </label>
                    </div>
                    <div class="form-control">
                        <label class="label label-primary">
                            <span class="label-text">Average Power</span>
                            <input
                                type="number"
                                min="0"
                                bind:value={averagePower}
                                class="input input-bordered"
                            />
                        </label>
                    </div>
                    <div class="form-control">
                        <label class="label label-primary">
                            <span class="label-text">Average Speed</span>
                            <input
                                type="number"
                                min="0"
                                bind:value={averageSpeed}
                                class="input input-bordered"
                            />
                        </label>
                    </div>
                    <div class="form-control">
                        <label class="label">
                            <span class="label-text">Ride Type</span>
                            <input
                                type="text"
                                maxlength="15"
                                bind:value={rideType}
                                class="input input-bordered"
                            />
                        </label>
                    </div>
                {:else if isLift}
                    <div class="form-control">
                        <label class="label label-primary">
                            <span class="label-text">Tonnage / Volume</span>
                            <input
                                type="number"
                                min="0"
                                bind:value={tonnage}
                                class="input input-bordered"
                            />
                        </label>
                    </div>
                    <div class="form-control">
                        <label class="label">
                            <span class="label-text">Lift Type</span>
                            <input
                                type="text"
                                maxlength="15"
                                bind:value={liftType}
                                class="input input-bordered"
                            />
                        </label>
                    </div>

                    <div class="form-control">
                        <label class="label">
                            <span class="label-text">Lift Focus</span>
                            <input
                                type="text"
                                maxlength="15"
                                bind:value={liftFocus}
                                class="input input-bordered"
                            />
                        </label>
                    </div>
                {:else if isRunWalk}
                    <div class="form-control">
                        <label class="label label-primary">
                            <span class="label-text">Distance</span>
                            <input
                                type="number"
                                min="0"
                                step="0.01"
                                bind:value={distance}
                                class="input input-bordered"
                            />
                        </label>
                    </div>
                    <div class="form-control">
                        <label class="label label-primary">
                            <span class="label-text">Elevation Gain</span>
                            <input
                                type="number"
                                min="0"
                                bind:value={elevationGain}
                                class="input input-bordered"
                            />
                        </label>
                    </div>
                    <div class="form-control">
                        <label class="label">
                            <span class="label-text"
                                >Average Pace (min, sec)</span
                            >
                            <div class="flex space-x-2">
                                <input
                                    type="number"
                                    min="0"
                                    max="59"
                                    bind:value={paceMinutes}
                                    class="input input-bordered w-15 mr-2"
                                    placeholder="min"
                                />
                                :
                                <input
                                    type="number"
                                    min="0"
                                    max="59"
                                    bind:value={paceSeconds}
                                    class="input input-bordered w-15"
                                    placeholder="sec"
                                />
                            </div>
                        </label>
                    </div>

                    <div class="form-control">
                        <label class="label">
                            <span class="label-text">Run/Walk Type</span>
                            <input
                                type="text"
                                maxlength="15"
                                bind:value={runType}
                                class="input input-bordered"
                            />
                        </label>
                    </div>
                {/if}

                <div class="divider font-bold mt-8">RPE PLUGIN</div>
                <div class="form-control">
                    <div>
                        <label
                            >Accumulated Fatigue
                            <input
                                type="range"
                                min="1"
                                max="5"
                                class="range mt-2"
                                step="1"
                                bind:value={accumulatedFatigue}
                            />
                            <div
                                class="flex w-full justify-between px-2 text-xs"
                            >
                                <span>nothing</span>
                                <span>low</span>
                                <span>normal</span>
                                <span>high</span>
                                <span>extreme</span>
                            </div>
                        </label>
                    </div>
                </div>

                <div class="form-control mt-8">
                    <div>
                        <label
                            >Difficulty Rating
                            <input
                                type="range"
                                min="1"
                                max="5"
                                class="range mt-2"
                                step="1"
                                bind:value={difficultyRating}
                            />
                            <div
                                class="flex w-full justify-between px-2 text-xs"
                            >
                                <span>1</span>
                                <span>2</span>
                                <span>3</span>
                                <span>4</span>
                                <span>5</span>
                            </div>
                        </label>
                    </div>
                </div>

                <div class="form-control mt-8">
                    <div>
                        <label
                            >Engagement Rating
                            <input
                                type="range"
                                min="1"
                                max="5"
                                class="range mt-2"
                                step="1"
                                bind:value={engagementRating}
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
                </div>

                <div class="form-control mt-8">
                    <label
                        >External Variables Rating
                        <input
                            type="range"
                            min="1"
                            max="5"
                            class="range mt-2"
                            step="1"
                            bind:value={externalVariablesRating}
                        />
                        <div class="flex w-full justify-between px-2 text-xs">
                            <span>bad</span>
                            <span>poor</span>
                            <span>fine</span>
                            <span>good</span>
                            <span>great</span>
                        </div>
                    </label>
                </div>

                <div class="form-control mt-10">
                    <label
                        >Summary / Notes <br /><textarea
                            bind:value={userSummary}
                            maxlength="200"
                            class="textarea textarea-bordered w-full"
                        ></textarea>
                    </label>
                </div>

                <div class="modal-action">
                    <button type="submit" class="btn btn-primary">Submit</button
                    >
                    <button type="button" class="btn" on:click={closeModal}
                        >Close</button
                    >
                </div>
            </form>
        </div>
    </div>
{/if}
