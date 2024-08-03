<script lang="ts">
    import { fetchLastWeekActivityMinutes, fetchTodaysActivities, todaysActivities } from "$lib/activityStore.ts";
    import ActivityTracker from "$lib/ActivityTracker.svelte";
    import ActivityViewer from "$lib/ActivityViewer.svelte";
  
    import { onMount } from "svelte";
    export let data;
    let lastWeekActivityMinutes = data.lastWeekActivityMinutes;

    onMount(async () => {
        fetchTodaysActivities(fetch);
        //lastWeekActivityMinutes = fetchLastWeekActivityMinutes(fetch)
    });

    async function updateMinutes(){
        lastWeekActivityMinutes = await fetchLastWeekActivityMinutes(fetch)
        console.log('hi')
    }
</script>

<svelte:head>
    <title>Activity Manager</title>
</svelte:head>

<div class="">
    <div class="mt-2 ml-2">
        <h1 class="text-6xl font-bold">Activity Viewer</h1>
        <div class="mt-5 w-10"><ActivityTracker/></div>
    </div>
    <div>
        <p>Last week activity minutes: {lastWeekActivityMinutes} min</p>
    </div>
    
</div>

<div class="divider"></div>
<div class="flex">
    
    {#each $todaysActivities as activity }
        <ActivityViewer {activity}/>
    {/each}
</div>
