<!-- <script lang="ts">
    import {
        fetchTodaysActivities,
        todaysActivities,
        lastWeeksActivityMinutes,
    } from "$lib/activityStore.ts";
    import ActivityTracker from "$lib/ActivityTracker.svelte";
    import ActivityViewer from "$lib/SingleActivityViewer.svelte";
    import { onMount } from "svelte";
    import { writable } from "svelte/store";

    let activityRatio = writable({
        numberLifts: 0,
        numberRunWalks: 0,
        numberRides: 0,
    });

    export async function fetchActivityRatio() {
        try {
            const endDate = new Date();
            const startDate = new Date();
            startDate.setDate(endDate.getDate() - (7*4));

            // Convert to strings in the year-month-day format
            const endString = endDate.toISOString().slice(0, 10); // "YYYY-MM-DD"
            const startString = startDate.toISOString().slice(0, 10); // "YYYY-MM-DD"

            const url =
                "api/activity/getactivitytyperatio?start=" +
                startString +
                "&end=" +
                endString;
            const response = await fetch(url);

            if (response.ok) {
                const data = await response.json();
                activityRatio.set(data)
            } else {
                console.error("Failed to get activity data");
            }
        } catch {
            console.error("Error fetching activity data");
        }
    }

    onMount(async () => {
        fetchActivityRatio()
    });

    function updatePage(){
        fetchTodaysActivities(fetch)
        fetchActivityRatio()
       
    }
</script>

<svelte:head>
    <title>Activity Manager</title>
</svelte:head>

<div class="">
    <div class="mt-2 ml-2 ">
        <h1 class="text-6xl font-bold ">Activity Viewer</h1>
        <div class="divider "> Relevant Data</div>
    </div>
</div>

<div class="flex ml-2 mt-4 ">
    <div class="stats shadow border border-primary">
        <div class="stat items-center">
            <button class="btn m-0 p-0" on:click={updatePage}>
                <ActivityTracker />
            </button>
            
        </div>
        <div class="stat">
            <div class="stat-title">Activity Minutes</div>
            <div class="stat-value">{lastWeeksActivityMinutes}</div>
            <div class="stat-desc">In the last 7 days</div>
        </div>
        <div class="stat">
            <div class="stat-title">Activity Ratio </div>
            <div class="stat-value">{$activityRatio.numberLifts}:{$activityRatio.numberRunWalks}:{$activityRatio.numberRides} </div>
            <div class="stat-desc">Lifts : Runs : Rides</div>
            <div class="stat-desc">In the last 4 weeks</div>
        </div>
    </div>
</div>

<div class="divider">Todays Activities</div>
<div class="flex flex-wrap items-center justify-center">
    {#each $todaysActivities as activity}
        <div class="p-2">
            <ActivityViewer {activity} />
        </div>
    {/each}
</div>

<!-- <footer class="">
    <div class="mt-5 w-10"><ActivityTracker/></div>
</footer> --> -->
