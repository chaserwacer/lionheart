<script lang="ts">
    import { fetchLastWeekActivityMinutes, fetchTodaysActivities, todaysActivities, lastWeeksActivityMinutes } from "$lib/activityStore.ts";
    import ActivityTracker from "$lib/ActivityTracker.svelte";
    import ActivityViewer from "$lib/ActivityViewer.svelte";
  
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
        <p>Last week activity minutes: ${lastWeeksActivityMinutes} min</p>
    </div>
    
</div>

<div class="divider"></div>
<div class="flex flex-wrap items-center justify-center">
    
    {#each $todaysActivities as activity }
    <div class="p-2">
        <ActivityViewer {activity}/>
    </div>
        
    {/each}
</div>

<!-- <footer class="">
    <div class="mt-5 w-10"><ActivityTracker/></div>
</footer> -->
