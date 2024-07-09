<script>
    import { onMount } from 'svelte';
    import Chart from 'chart.js/auto';
  
    /**
     * @type {typeof import("svelte-chartjs").Line}
     */
    let Line;
    let overallWellness = 3.5;
    let energy = 4;
    let motivation = 3;
    let mood = 5;
    let stress = 2;
    let trackedToday = false;
  
    const pastWeekData = [3, 4, 4, 5, 3.5, 4.2, 3.8];
    const pastTwoWeeksData = [3.5, 4.1, 3.8, 4.3, 4, 3.9, 4.5, 3.5, 4, 4.2, 3.7, 4.1, 3.9, 4];
  
    const weekData = {
      labels: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
      datasets: [{
        label: 'Overall Wellness (Past Week)',
        data: pastWeekData,
        fill: false,
        borderColor: 'rgb(75, 192, 192)',
        tension: 0.1
      }]
    };
  
    const twoWeeksData = {
      labels: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
      datasets: [{
        label: 'Overall Wellness (Past Two Weeks)',
        data: pastTwoWeeksData,
        fill: false,
        borderColor: 'rgb(75, 192, 192)',
        tension: 0.1
      }]
    };
  
    function trackToday() {
      trackedToday = true;
    }
  
    onMount(async () => {
      const module = await import('svelte-chartjs');
      Line = module.Line;
    });
  </script>
  
  <svelte:head>
    <title>Wellness Tracker</title>
  </svelte:head>
  
  {#if Line}
  <input type="checkbox" value="synthwave" class="toggle theme-controller" />
    <div class="container mx-auto p-2">
      <div class="text-center mb-4">
        <h1 class="text-5xl font-bold">Wellness Tracker</h1>
      </div>
  
      <div class="flex justify-center mb-6 text-center">
        <div class="stat bg-primary text-primary-content">
          <div class="stat-value hover:underline">Overall Wellness</div>
          <div class="stat-value">{overallWellness} / 5</div>
        </div>
      </div>
  
      <div class="grid grid-cols-2 gap-4 mb-6 ">
        <div class="stat bg-accent text-accent-content">
          <div class="stat-value hover:underline">Energy</div>
          <div class="stat-value">{energy} / 5</div>
        </div>
        <div class="stat bg-accent text-accent-content">
          <div class="stat-value hover:underline">Motivation</div>
          <div class="stat-value">{motivation} / 5</div>
        </div>
        <div class="stat bg-accent text-accent-content">
          <div class="stat-value hover:underline">Mood</div>
          <div class="stat-value">{mood} / 5</div>
        </div>
        <div class="stat bg-accent text-accent-content">
          <div class="stat-value hover:underline">Stress</div>
          <div class="stat-value">{stress} / 5</div>
        </div>
      </div>
  
      <div class="flex justify-center mb-6">
        <button class="btn btn-primary" on:click={trackToday} disabled={trackedToday}>
          {trackedToday ? 'Tracked Today' : 'Track Today'}
        </button>
      </div>
  
      <div class="mb-6">
        <h2 class="text-2xl font-bold mb-4 hover:underline">Past Week Overview</h2>
        <Line data={weekData} />
      </div>
  
      <div>
        <h2 class="text-2xl font-bold mb-4 hover:underline">Past Two Weeks Overview</h2>
        <Line data={twoWeeksData} />
      </div>
    </div>
  {/if}
