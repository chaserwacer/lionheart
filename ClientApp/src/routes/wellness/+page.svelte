<script>
  import { onMount } from "svelte";
  import Chart from "chart.js/auto";
    import { writable } from "svelte/store";

  /**
   * @type {typeof import("svelte-chartjs").Line}
   */
  let Line;
  let showModal = writable(false);
  let overallWellness = 3.5;
  let energy = 4;
  let motivation = 3;
  let mood = 5;
  let stress = 2;
  let trackedToday = false;

  const pastWeekData = [3, 4, 4, 5, 3.5, 4.2, 3.8];
  const pastTwoWeeksData = [
    3.5, 4.1, 3.8, 4.3, 4, 3.9, 4.5, 3.5, 4, 4.2, 3.7, 4.1, 3.9, 4,
  ];

  const weekData = {
    labels: ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"],
    datasets: [
      {
        label: "Overall Wellness (Past Week)",
        data: pastWeekData,
        fill: false,
        borderColor: "rgb(75, 192, 192)",
        tension: 0.1,
      },
    ],
  };

  const twoWeeksData = {
    labels: [
      "Mon",
      "Tue",
      "Wed",
      "Thu",
      "Fri",
      "Sat",
      "Sun",
      "Mon",
      "Tue",
      "Wed",
      "Thu",
      "Fri",
      "Sat",
      "Sun",
    ],
    datasets: [
      {
        label: "Overall Wellness (Past Two Weeks)",
        data: pastTwoWeeksData,
        fill: false,
        borderColor: "rgb(75, 192, 192)",
        tension: 0.1,
      },
    ],
  };

  function openModal() {
    showModal.set(true);
  }

  function closeModal() {
    showModal.set(false);
  }

  onMount(async () => {
    const module = await import("svelte-chartjs");
    Line = module.Line;
  });

  async function trackScores(){
    try{

    }catch (error){
      console.error("Error tracking scores", error)
    }
  }
</script>

<svelte:head>
  <title>Wellness Tracker</title>
</svelte:head>

<!-- <input type="checkbox" value="synthwave" class="toggle theme-controller" /> -->
<div class="text-center mt-5">
  <h1 class="text-5xl font-bold text-primary">Wellness Tracker</h1>
</div>
<div class="divider divider-neutral"></div>

<div class="flex flex-col ml-20 mr-20">
  <div class="stats shadow mb-5">
    <div class="stat bg-primary text-primary-content place-items-center">
      <div class="stat-value">Wellness Score</div>
      <div class="stat-value">{overallWellness} / 5</div>
    </div>
  </div>

  <div
    class="stats stats-vertical md:stats-horizontal shadow bg-secondary text-secondary-content flex-initial mb-5"
  >
    <div class="stat place-items-center">
      <div
        class="radial-progress"
        style="--value:80; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title ">Energy</div>
        <div class="stat-value">{energy} / 5</div>
        <div class="stat-desc">good</div>
      </div>
    </div>

    <div class="stat place-items-center">
      <div
        class="radial-progress"
        style="--value:60; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title">Motivation</div>
        <div class="stat-value">{motivation} / 5</div>
        <div class="stat-desc">okay</div>
      </div>
    </div>

    <div class="stat place-items-center">
      <div
        class="radial-progress"
        style="--value:100; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title">Mood</div>
        <div class="stat-value">{mood} / 5</div>
        <div class="stat-desc">great</div>
      </div>
    </div>
    <div class="stat place-items-center">
      <div
        class="radial-progress"
        style="--value:40; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title">Stress</div>
        <div class="stat-value">{stress} / 5</div>
        <div class="stat-desc">bad</div>
      </div>
    </div>
  </div>

  <div class="flex justify-center"><button class="btn btn-neutral btn-lg" on:click={openModal}>Open Daily Tracker</button></div>
  

{#if $showModal}
  <div class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center">
    <div class="modal modal-open">
      <div class="modal-box">
        <h2 class="font-bold text-lg">Daily Tracker</h2>
        <p>Please fill out these values during the first quarter of your day. Answer with honest values - you will not be able to modify your values today and will only track them once.</p>

        <form class="space-y-4 mt-4">
          <div>
            <label>Energy
              <input type="range" min="1" max="5" class="range" step="1" bind:value={energy} />
            <div class="flex w-full justify-between px-2 text-xs">
              <span>nonexistent</span>
              <span>low</span>
              <span>normal</span>
              <span>high</span>
              <span>extreme</span>
            </div>
            </label>
            
          </div>

          <div>
            <label>Motivation
              <input type="range" min="1" max="5"  class="range" step="1" bind:value={motivation} />
              <div class="flex w-full justify-between px-2 text-xs">
                <span>nonexistent</span>
                <span>low</span>
                <span>normal</span>
                <span>high</span>
                <span>extreme</span>
              </div>
            </label>
            
          </div>

          <div>
            <label>Mood
              <input type="range" min="1" max="5" class="range" step="1" bind:value={mood} />
            <div class="flex w-full justify-between px-2 text-xs">
              <span>terrible</span>
              <span>bad</span>
              <span>okay</span>
              <span>good</span>
              <span>great</span>
            </div>
            </label>
          </div>

          <div>
            <label>Stress
              <input type="range" min="1" max="5" class="range" step="1" bind:value={stress} />
            <div class="flex w-full justify-between px-2 text-xs">
              <span>nonexistent</span>
              <span>low</span>
              <span>normal</span>
              <span>high</span>
              <span>extreme</span>
            </div>
            </label>
            
          </div>

          <div class="modal-action">
            <button type="button" class="btn btn-neutral" on:click={trackScores}>Track Scores</button>
            <button type="button" class="btn" on:click={closeModal}>Cancel</button>
          </div>
        </form>
      </div>
    </div>
  </div>
{/if}
</div>

<div class="divider divider-neutral text-base-content"></div>
{#if Line}
  <div class="flex flex-col items-center justify-center">
    <div class="w-10/12 text-center">
      <h2 class="text-2xl font-bold mb-4 hover:underline ">
        Past Week Overview
      </h2>
      <Line data={weekData} />
    </div>

    <div class="w-10/12 text-center">
      <h2 class="text-2xl font-bold mb-4 hover:underline">
        Past Two Weeks Overview
      </h2>
      <Line data={twoWeeksData} />
    </div>
  </div>
{/if}
