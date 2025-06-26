<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import {
    bootUserDto,
    fetchBootUserDto,
    type WellnessState,
    pageUpdate,
    wellnessStateDate,
  } from "$lib/stores/stores.js";

  import ActivityTracker from "$lib/components/ActivityTracker.svelte";
  import WellnessTracker from "$lib/components/WellnessTracker.svelte";
  import { writable } from "svelte/store";
  import Layout from "./+layout.svelte";
  import ActivityViewer from "$lib/components/SingleActivityViewer.svelte";
  import Chart from "chart.js/auto";
  import type { Line } from "svelte-chartjs";
  import type { Activity, MuscleSetsDto } from "$lib/stores/activityStore.js";
  import {
    fetchActivities,
    fetchActivityMinutes,
    fetchActivityRatio,
    fetchWeeklyMuscleSetsDto,
  } from "$lib/stores/activityStore.js";
  import SingleActivityViewer from "$lib/components/SingleActivityViewer.svelte";
  import { syncOuraData, GetDailyOuraInfo } from "$lib/stores/ouraStore.js";

  /**
   * @type {typeof import("svelte-chartjs").Line}
   */
  let wellnessGraph: typeof import("svelte-chartjs").Line;

  let wellnessGraphData = {
    labels: [],
    datasets: [
      {
        label: "Last Weeks Wellness Scores",
        data: [],
        fill: false,
        borderColor: "rgb(75, 192, 192)",
        tension: 0.1,
      },
    ],
  };

  // --- CHANGED: updateWellnessGraph now uses new endpoint and request query ---
  async function updateWellnessGraph() {
    // Old: POST to /api/wellness/get-range with body
    // New: GET to /api/wellness/get-range?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD
    const endDate = selectedDate;
    const startDateObj = new Date(selectedDate);
    startDateObj.setDate(startDateObj.getDate() - 6);
    const startDate = startDateObj.toISOString().slice(0, 10);

    const url = `api/wellness/get-range?startDate=${encodeURIComponent(startDate)}&endDate=${encodeURIComponent(endDate)}`;
    const response = await fetch(url, {
      method: "GET",
      headers: { "Content-Type": "application/json" }
    });

    if (!response.ok) {
      console.error("Failed to get wellness states for graph");
      return;
    }
    const data = await response.json();
    // Assume data is an array of WellnessState objects with overallScore and date
    const scores = data.map((ws: { overallScore: any; }) => ws.overallScore);
    const dates = data.map((ws: { date: any; }) => ws.date);

    wellnessGraphData = {
      labels: dates,
      datasets: [
        {
          label: "Last Weeks Wellness Scores",
          data: scores,
          fill: false,
          borderColor: "rgb(75, 192, 192)",
          tension: 0.1,
        },
      ],
    };
  }

  export let data;
  let lastUpdatePage = $pageUpdate;
  let selectedDate = new Date().toISOString().slice(0, 10);
  let weeklyMuscleSets: MuscleSetsDto = {
    quadSets: 0,
    hamstringSets: 0,
    bicepSets: 0,
    tricepSets: 0,
    shoulderSets: 0,
    chestSets: 0,
    backSets: 0,
  };
  const wellnessState = writable({
    motivationScore: 0,
    stressScore: 0,
    moodScore: 0,
    energyScore: 0,
    overallScore: -1,
    date: "",
  });
  let lastWeeksActivityMinutes: number;
  const activityTypeRatio = writable({
    numberLifts: 0,
    numberRunWalks: 0,
    numberRides: 0,
  });

  onMount(async () => {
    await fetchBootUserDto(fetch);
    if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
      goto("/auth");
    } else {
      await fetchWellnessState();
      wellnessStateDate.set(selectedDate);
      const module = await import("svelte-chartjs");
      wellnessGraph = module.Line;
      updateWellnessGraph();
      activities.set(await fetchActivities(selectedDate, selectedDate, fetch));
      lastWeeksActivityMinutes = await fetchActivityMinutes(
        selectedDate,
        fetch,
      );
      activityTypeRatio.set(await fetchActivityRatio(selectedDate, fetch));
      weeklyMuscleSets = await fetchWeeklyMuscleSetsDto(selectedDate, fetch);
      dailyOuraInfo.set(await GetDailyOuraInfo(selectedDate, fetch));
    }
  });

  // --- CHANGED: fetchWellnessState now uses new endpoint URL ---
  async function fetchWellnessState() {
    try {
      // Old: "api/user/getwellnessstate?date=" + selectedDate
      // New: "api/wellness/get?date=" + selectedDate
      const url = "api/wellness/get?date=" + selectedDate;
      const response = await fetch(url);

      if (response.ok) {
        const data = await response.json();

        wellnessState.set(data);
      } else {
        console.error("Failed to get  Wellness State");
      }
    } catch {
      console.error("Error fetching  wellness state");
    }
  }

  const dailyOuraInfo = writable({
    objectID: "",
    date: "", // Assuming DateOnly is a string representation
    resilienceData: {
      sleepRecovery: 0,
      daytimeRecovery: 0,
      stress: 0,
      resilienceLevel: 0,
    },
    activityData: {
      activityScore: 0,
      steps: 0,
      activeCalories: 0,
      totalCalories: 0,
      targetCalories: 0,
      meetDailyTargets: 0,
      moveEveryHour: 0,
      recoveryTime: 0,
      stayActive: 0,
      trainingFrequency: 0,
      trainingVolume: 0,
    },
    sleepData: {
      sleepScore: 0,
      deepSleep: 0,
      efficiency: 0,
      latency: 0,
      remSleep: 0,
      restfulness: 0,
      timing: 0,
      totalSleep: 0,
    },
    readinessData: {
      readinessScore: 0,
      temperatureDeviation: 0,
      activityBalance: 0,
      bodyTemperature: 0,
      hrvBalance: 0,
      previousDayActivity: 0,
      previousNight: 0,
      recoveryIndex: 0,
      restingHeartRate: 0,
      sleepBalance: 0,
    },
  });
  const activities = writable<Activity[]>([]);

  async function updatePageInfo() {
    wellnessStateDate.set(selectedDate);
    console.log("Update page info on data: ", selectedDate, $wellnessStateDate);
    fetchWellnessState();
    activities.set(await fetchActivities(selectedDate, selectedDate, fetch));
    updateWellnessGraph();
    lastWeeksActivityMinutes = await fetchActivityMinutes(selectedDate, fetch);
    activityTypeRatio.set(await fetchActivityRatio(selectedDate, fetch));
    weeklyMuscleSets = await fetchWeeklyMuscleSetsDto(selectedDate, fetch);
    dailyOuraInfo.set(await GetDailyOuraInfo(selectedDate, fetch));
  }

  $: {
    if (lastUpdatePage != $pageUpdate) {
      updatePageInfo();
    }
  }
</script>

<svelte:head>
  <title>Lionheart</title>
</svelte:head>

<div class="flex flex-row">
  <article class="prose max-w-none pl-5 pt-5 text-center">
    <h1 class="mb-2">Lionheart Homebase</h1>
    <div class="flex flex-col md:flex-row items-center md:items-start">
      <h3 class="p-0 m-0 md:w-1/2">
        Welcome, {$bootUserDto.name}.
      </h3>

      <div class="divider-horizontal divider"></div>
      <div class="card items-center p-0 m-0 md:w-1/2 items-center">
        <div class="card-body items-center p-0 m-0">
          <h2 class="card-title p-0 m-0 md:mt-0 text-accent">Selected Date</h2>
          <input
            type="date"
            bind:value={selectedDate}
            on:change={updatePageInfo}
            class="btn btn-accent"
          />
        </div>
      </div>
    </div>
  </article>

  <div class="divider divider-horizontal"></div>
  <div class="w-1/2 mx-auto flex items-center justify-center">
    <article class="prose max-w-none text-left">
      <h1 class="text-accent">{data.post.advice}</h1>
    </article>
  </div>
</div>

<div class="divider">Wellness | Oura Overview</div>
<div class="flex flex-col lg:flex-row">
  <div class="flex flex-col text-xs lg:w-1/2">
    <div
      class=" hover:shadow-xl m-5 mt-0 stats stats-vertical lg:stats-horizontal shadow bg-accent text-primary-content flex-initial {$wellnessState.overallScore ===
      -1
        ? 'blur-sm'
        : ''}"
    >
      <div class="stat place-items-center text-center">
        <div class="stat-title text-xl text-primary-content">
          Wellness State <br /> Overall Score
        </div>
        <div class="stat-value text-primary-content text-xl">
          {$wellnessState.overallScore} / 5
        </div>
      </div>
      <!-- <div class="divider divider-accent divider-horizontal "></div> -->
      <div class="stat place-items-center p-1">
        <div
          class="radial-progress text-primary-content"
          style="--value:{($wellnessState.energyScore / 5) *
            100}; --size:6rem; --thickness: 2px;"
          role="progressbar"
        >
          <div class="stat-title text-primary-content">Energy</div>
          <div class="stat-value text-primary-content text-xl">
            {$wellnessState.energyScore} / 5
          </div>
          <div class="stat-desc text-primary-content">énergie</div>
        </div>
      </div>

      <div class="stat place-items-center p-1">
        <div
          class="radial-progress text-primary-content"
          style="--value:{($wellnessState.motivationScore / 5) *
            100}; --size:6rem; --thickness: 2px;"
          role="progressbar"
        >
          <div class="stat-title text-primary-content">Motivation</div>
          <div class="stat-value text-primary-content text-xl">
            {$wellnessState.motivationScore} / 5
          </div>
          <div class="stat-desc text-primary-content">motif</div>
        </div>
      </div>

      <div class="stat place-items-center p-1">
        <div
          class="radial-progress text-primary-content"
          style="--value:{($wellnessState.moodScore / 5) *
            100}; --size:6rem; --thickness: 2px;"
          role="progressbar"
        >
          <div class="stat-title text-primary-content">Mood</div>
          <div class="stat-value text-primary-content text-xl">
            {$wellnessState.moodScore} / 5
          </div>
          <div class="stat-desc text-primary-content">humeur</div>
        </div>
      </div>
      <div class="stat place-items-center p-1">
        <div
          class="radial-progress text-primary-content"
          style="--value:{($wellnessState.stressScore / 5) *
            100}; --size:6rem; --thickness: 2px;"
          role="progressbar"
        >
          <div class="stat-title text-primary-content">Stress</div>
          <div class="stat-value text-primary-content text-xl">
            {$wellnessState.stressScore} / 5
          </div>
          <div class="stat-desc text-primary-content">soulignez</div>
        </div>
      </div>
    </div>

    <div
      class="hover:shadow-xl m-5 mt-0 stats stats-vertical lg:stats-horizontal shadow bg-accent-content text-accent flex-initial text-center lg:text-start"
    >
      <div class="stat">
        <div class="stat-value">Oura Scores</div>
        <div class="stat-desc text-accent">OURA RING</div>
        <div class="stat-desc text-accent">{$dailyOuraInfo.date}</div>
      </div>
      <div class="stat">
        <div class="stat-title text-accent text-lg">Readiness</div>

        <div class="stat-value">
          {$dailyOuraInfo.readinessData.readinessScore}
        </div>
      </div>
      <div class="stat">
        <div class="stat-title text-lg text-accent">Sleep</div>

        <div class="stat-value">
          {$dailyOuraInfo.sleepData.sleepScore}
        </div>
      </div>
      <div class="stat">
        <div class="stat-title text-lg text-accent">Activity</div>

        <div class="stat-value">
          {$dailyOuraInfo.activityData.activityScore}
        </div>
      </div>
    </div>
  </div>
  {#if wellnessGraph}
    <div class="divider divider-horizontal"></div>
    <div class="text-center lg:w-1/2 mx-auto w-full">
      <!-- Adjust the size of the graph container here -->
      <div class="w-full">
        <h2 class="text-2xl font-bold hover:underline text-accent">
          Past Week Wellness Overview
        </h2>
        <!-- Example width and height -->
        <svelte:component this={wellnessGraph} data={wellnessGraphData} />
      </div>
    </div>
  {/if}
</div>

<div class="divider">Activity</div>
<div class="flex flex flex-col lg:flex-row items-center lg:items-start">
  <div class="flex flex-wrap justify-center">
    <div
      class=" bg-primary text-primary-content m-2 rounded-lg hover:shadow-xl"
    >
      <h1 class="m-2 text-xl font-bold">
        Today's Activities <label
          for="my_modal_7"
          class="btn btn-xs mt-2 ml-2 {$activities.length === 0
            ? 'btn-disabled'
            : ''}">view</label
        >
      </h1>

      <input type="checkbox" id="my_modal_7" class="modal-toggle" />
      <div class="modal" role="dialog">
        <div class="modal-box bg-white text-black flex flex-col">
          {#each $activities as activity}
            <h1 class="text-2xl font-bold text-center">Activity Viewer</h1>
            <div class="divider divider-primary m-0"></div>
            <SingleActivityViewer {activity} />
          {/each}
        </div>
        <label class="modal-backdrop" for="my_modal_7">Close</label>
      </div>
      <table class="table">
        <!-- head -->
        <thead class="text-primary-content">
          <tr>
            <th>Name</th>
            <th>Type</th>
            <th>Difficulty</th>
            <th>Duration</th>
          </tr>
        </thead>
        <tbody>
          <!-- row 1 -->
          {#each $activities as activity}
            <tr>
              <!-- Start Modal -->

              <!-- End Modal -->
              <td>{activity.name}</td>
              {#if activity.liftDetails != null}
                <td>Lift</td>
              {:else if activity.rideDetails != null}
                <td>Ride</td>
              {:else if activity.runWalkDetails != null}
                <td>Run/Walk</td>
              {:else}
                <td>Base Activity</td>
              {/if}
              <td>{activity.difficultyRating} / 5</td>
              <td>{activity.timeInMinutes} min</td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>

    <div class="flex m-2">
      <div
        class="stats shadow bg-primary text-primary-content hover:shadow-xl m-0"
      >
        <div class="stat">
          <div class="stat-title text-primary-content">Activity Minutes</div>
          <div class="stat-value">{lastWeeksActivityMinutes}</div>
          <div class="stat-desc text-primary-content">In the last 7 days</div>
        </div>
        <div class="stat">
          <div class="stat-title text-primary-content">Activity Ratio</div>
          <div class="stat-value">
            {$activityTypeRatio.numberLifts}:{$activityTypeRatio.numberRunWalks}:{$activityTypeRatio.numberRides}
          </div>
          <div class="stat-desc text-primary-content">Lifts : Runs : Rides</div>
          <div class="stat-desc text-primary-content">In the last 4 weeks</div>
        </div>
      </div>
    </div>
  </div>
  <div class="divider divider-horizontal"></div>
  <div class="bg-primary text-primary-content rounded-lg m-2 hover:shadow-xl">
    <table class="table table-sm font-bold">
      <!-- head -->
      <thead>
        <tr>
          <th class="font-bold text-lg text-primary-content shadow"
            >Muscle Group</th
          >
          <th class="font-bold text-lg text-primary-content shadow"
            >Sets in past 7 days</th
          >
        </tr>
      </thead>
      <tbody>
        <!-- quads row -->
        <tr>
          <td>Quads</td>
          <td>{weeklyMuscleSets.quadSets}</td>
        </tr>
        <!-- hamstrings row -->
        <tr>
          <td>Hamstrings</td>
          <td>{weeklyMuscleSets.hamstringSets}</td>
        </tr>
        <!-- biceps row -->
        <tr>
          <td>Biceps</td>
          <td>{weeklyMuscleSets.bicepSets}</td>
        </tr>
        <!-- triceps row -->
        <tr>
          <td>Triceps</td>
          <td>{weeklyMuscleSets.tricepSets}</td>
        </tr>
        <!-- shoulders row -->
        <tr>
          <td>Shoulders</td>
          <td>{weeklyMuscleSets.shoulderSets}</td>
        </tr>
        <!-- chest row -->
        <tr>
          <td>Chest</td>
          <td>{weeklyMuscleSets.chestSets}</td>
        </tr>
        <!-- back row -->
        <tr>
          <td>Back</td>
          <td>{weeklyMuscleSets.backSets}</td>
        </tr>
      </tbody>
    </table>
  </div>
</div>

<div class="divider">Oura</div>
<div

  class="flex flex-wrap items-center justify-center lg:items-start lg:justify-start"
>
  <div class="card bg-base-300 w-80 shadow-xl m-5 indicator">
    <span class="indicator-item badge badge-info">oura</span>
    <div class="card-body">
      <h2 class="card-title text-5xl">Resilience</h2>
      <p>Level:</p>
      <p class=" text-3xl font-bold">
        {$dailyOuraInfo.resilienceData.resilienceLevel}
      </p>

      Daytime Recovery:<progress
        class="progress"
        value={$dailyOuraInfo.resilienceData.daytimeRecovery}
        max="100"
      ></progress>
      Sleep Recovery:
      <progress
        class="progress"
        value={$dailyOuraInfo.resilienceData.sleepRecovery}
        max="100"
      ></progress>
      Stress:
      <progress
        class="progress"
        value={$dailyOuraInfo.resilienceData.stress}
        max="100"
      ></progress>
    </div>
  </div>
  <div class="card bg-base-300 w-80 shadow-xl m-5 indicator">
    <div class="card-body">
      <span class="indicator-item badge badge-info">oura</span>
      <h2 class="card-title text-5xl">Sleep</h2>
      <h2 class="text-4xl">{$dailyOuraInfo.sleepData.sleepScore}</h2>
      Total Sleep:<progress
        class="progress"
        value={$dailyOuraInfo.sleepData.totalSleep}
        max="100"
      ></progress>
      Efficiency:<progress
        class="progress"
        value={$dailyOuraInfo.sleepData.efficiency}
        max="100"
      ></progress>
      Restfullness:<progress
        class="progress"
        value={$dailyOuraInfo.sleepData.restfulness}
        max="100"
      ></progress>
      Timing:<progress
        class="progress"
        value={$dailyOuraInfo.sleepData.timing}
        max="100"
      ></progress>
      Latency:<progress
        class="progress"
        value={$dailyOuraInfo.sleepData.latency}
        max="100"
      ></progress>
      Rem Sleep:<progress
        class="progress"
        value={$dailyOuraInfo.sleepData.remSleep}
        max="100"
      ></progress>
      Deep Sleep:<progress
        class="progress"
        value={$dailyOuraInfo.sleepData.deepSleep}
        max="100"
      ></progress>
    </div>
  </div>

  <div class="card bg-base-300 w-80 shadow-xl m-5 indicator">
    <div class="card-body">
      <span class="indicator-item badge badge-info">oura</span>
      <h2 class="card-title text-5xl">Activity</h2>
      <h2 class="text-4xl">{$dailyOuraInfo.activityData.activityScore}</h2>
      <table class="table">
        <!-- head -->
        <thead class="text-primary-content"> </thead>
        <tbody>
          <tr>
            <td>Steps</td>
            <td>{$dailyOuraInfo.activityData.steps}</td>
          </tr>
          <tr>
            <td>Active Cals</td>
            <td>{$dailyOuraInfo.activityData.activeCalories} </td>
          </tr>
          <tr>
            <td>Total Calories</td>
            <td>{$dailyOuraInfo.activityData.totalCalories}</td>
          </tr>
        </tbody>
      </table>
      Training Frequency:<progress
        class="progress"
        value={$dailyOuraInfo.activityData.trainingFrequency}
        max="100"
      ></progress>
      Training Volume:<progress
        class="progress"
        value={$dailyOuraInfo.activityData.trainingVolume}
        max="100"
      ></progress>
      Recovery Time:<progress
        class="progress"
        value={$dailyOuraInfo.activityData.recoveryTime}
        max="100"
      ></progress>
      Stay Active:<progress
        class="progress"
        value={$dailyOuraInfo.activityData.stayActive}
        max="100"
      ></progress>
      Move Every Hour:<progress
        class="progress"
        value={$dailyOuraInfo.activityData.moveEveryHour}
        max="100"
      ></progress>
      Meet Daily Targets:<progress
        class="progress"
        value={$dailyOuraInfo.activityData.meetDailyTargets}
        max="100"
      ></progress>
    </div>
  </div>

  <div class="card bg-base-300 w-80 shadow-xl m-5 indicator">
    <div class="card-body">
      <span class="indicator-item badge badge-info">oura</span>
      <h2 class="card-title text-5xl">Readiness</h2>
      <h2 class="text-4xl">{$dailyOuraInfo.readinessData.readinessScore}</h2>
      <table class="table">
        <!-- head -->
        <thead class="text-primary-content"> </thead>
        <tbody>
          <tr>
            <td>Temperature Deviation</td>
            <td>{$dailyOuraInfo.readinessData.temperatureDeviation}</td>
          </tr>
        </tbody>
      </table>
      Sleep Balance:<progress
        class="progress"
        value={$dailyOuraInfo.readinessData.sleepBalance}
        max="100"
      ></progress>
      Activity Balance:<progress
        class="progress"
        value={$dailyOuraInfo.readinessData.activityBalance}
        max="100"
      ></progress>
      Recovery Index:<progress
        class="progress"
        value={$dailyOuraInfo.readinessData.recoveryIndex}
        max="100"
      ></progress>
      Body Temperature:<progress
        class="progress"
        value={$dailyOuraInfo.readinessData.bodyTemperature}
        max="100"
      ></progress>
      HRV Balance:<progress
        class="progress"
        value={$dailyOuraInfo.readinessData.hrvBalance}
        max="100"
      ></progress>
      Previous Night:<progress
        class="progress"
        value={$dailyOuraInfo.readinessData.previousNight}
        max="100"
      ></progress>
      Previoys Day Activiy:<progress
        class="progress"
        value={$dailyOuraInfo.readinessData.previousDayActivity}
        max="100"
      ></progress>

      Resting Heart Rate:<progress
        class="progress"
        value={$dailyOuraInfo.readinessData.restingHeartRate}
        max="100"
      ></progress>
    </div>
  </div>
</div>
