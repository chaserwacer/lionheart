<script>
    import ActivityTracker from "$lib/ActivityTracker.svelte";
    import { fetchTodaysActivities } from "$lib/stores";
    import { onMount } from "svelte";

    onMount(async () => {
        fetchTodaysActivities(fetch)
  });
</script>

<svelte:head>
    <title>Activity Manager</title>
</svelte:head>


<div class="">
    <div class="mt-5 ml-1">
        <h1 class="text-6xl font-bold">Activity Viewer</h1>
        <div class="w-10"><ActivityTracker/></div>
        
    </div>
</div>

<div class="divider"></div>
