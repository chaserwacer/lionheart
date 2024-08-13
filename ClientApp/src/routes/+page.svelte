<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import {
    bootUserDto,
    fetchBootUserDto,
    type WellnessState,
    pageUpdate,
  } from "$lib/stores";

  import ActivityTracker from "$lib/ActivityTracker.svelte";
  import WellnessTracker from "$lib/WellnessTracker.svelte";
  import { writable } from "svelte/store";
  import Layout from "./+layout.svelte";
  import ActivityViewer from "$lib/SingleActivityViewer.svelte";
  import Chart from "chart.js/auto";
  import type { Line } from "svelte-chartjs";
  import type { Activity } from "$lib/activityStore";
  import {
    fetchActivities,
    fetchActivityMinutes,
    fetchActivityRatio,
  } from "$lib/activityStore.js";
    import SingleActivityViewer from "$lib/SingleActivityViewer.svelte";

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

  async function updateWellnessGraph() {
    const response = await fetch(
      "api/user/GetLastXWellnessStatesGraphData?xDays=7",
    );
    if (!response.ok) {
      console.error("Failed to get todays Wellness State");
    }
    const data = await response.json();
    const { scores, dates } = data;

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
    }
    await fetchWellnessState();
    const module = await import("svelte-chartjs");
    wellnessGraph = module.Line;
    updateWellnessGraph();
    activities.set(await fetchActivities(selectedDate, selectedDate, fetch));
    lastWeeksActivityMinutes = await fetchActivityMinutes(selectedDate, fetch);
    activityTypeRatio.set(await fetchActivityRatio(selectedDate, fetch));
  });

  async function fetchWellnessState() {
    try {
      const url = "api/user/getwellnessstate?date=" + selectedDate;
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
  const activities = writable<Activity[]>([]);

  async function updatePageInfo() {
    console.log("Update page info on data: ", selectedDate);
    fetchWellnessState();
    activities.set(await fetchActivities(selectedDate, selectedDate, fetch));
    updateWellnessGraph();
    lastWeeksActivityMinutes = await fetchActivityMinutes(selectedDate, fetch);
    activityTypeRatio.set(await fetchActivityRatio(selectedDate, fetch));
  }

  $: {
    if (lastUpdatePage != $pageUpdate) {
      updatePageInfo();
    }
  }
</script>

<svelte:head>
  <title>homebase</title>
</svelte:head>

<div class="flex flex-row">
  <article class="prose max-w-none pl-5 pt-5">
    <h1 class="mb-2">Lionheart Homebase</h1>
    <div class="flex flex-row">
      <h3 class="p-0 m-0 w-1/2">
        Welcome, {$bootUserDto.name}.
      </h3>
      <div class="divider-horizontal divider"></div>
      <div class="card items-center p-0 m-0 w-1/2 items-center">
        <div class="card-body items-center p-0 m-0">
          <h2 class="card-title p-0 m-0 text-accent">Selected Date</h2>
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

<div class="divider">Wellness</div>
<div class="flex flex-row">
  <div class="flex flex-col text-xs w-1/2">
    <div
      class=" m-5 mt-0 stats stats-vertical md:stats-horizontal shadow bg-primary text-primary-content flex-initial {$wellnessState.overallScore ===
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
  </div>
  {#if wellnessGraph}
    <div class="divider divider-horizontal"></div>
    <div class="text-center w-1/3 mx-auto">
      <h2 class="text-2xl font-bold hover:underline">
        Past Week Wellness Overview
      </h2>
      <!-- Adjust the size of the graph container here -->
      <div class="w-full">
        <!-- Example width and height -->
        <svelte:component this={wellnessGraph} data={wellnessGraphData} />
      </div>
    </div>
  {/if}
</div>

<div class="divider">Activity</div>

<div class="flex flex-col md:flex-row items-center md:items-start">
  <div class=" bg-accent text-accent-content m-2 rounded-lg">
    <h1 class="ml-5 mt-2 text-xl font-bold">Today's Activities</h1>
    <table class="table">
      <!-- head -->
      <thead class="text-accent-content">
        <tr>
          <th></th>
          <th>Name</th>
          <th>Type</th>
          <th>Difficulty</th>
          <th>Length</th>
        </tr>
      </thead>
      <tbody>
        <!-- row 1 -->
        {#each $activities as activity}
          <tr>
            <!-- Start Modal -->
            <label for="my_modal_7" class="btn btn-xs mt-2 ml-2">view</label>
            <input type="checkbox" id="my_modal_7" class="modal-toggle" />
            <div class="modal" role="dialog">
              <div class="modal-box bg-white text-black">
                <h1 class="text-xl font-bold ">Activity Viewer</h1>
                <div class="divider divider-accent m-0"></div>
                <SingleActivityViewer {activity} />
              </div>
              <label class="modal-backdrop" for="my_modal_7">Close</label>
            </div>

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
    <div class="stats shadow bg-accent text-accent-content">
      <div class="stat">
        <div class="stat-title text-accent-content">Activity Minutes</div>
        <div class="stat-value">{lastWeeksActivityMinutes}</div>
        <div class="stat-desc text-accent-content">In the last 7 days</div>
      </div>
      <div class="stat">
        <div class="stat-title text-accent-content">Activity Ratio</div>
        <div class="stat-value">
          {$activityTypeRatio.numberLifts}:{$activityTypeRatio.numberRunWalks}:{$activityTypeRatio.numberRides}
        </div>
        <div class="stat-desc text-accent-content">Lifts : Runs : Rides</div>
        <div class="stat-desc text-accent-content">In the last 4 weeks</div>
      </div>
    </div>
  </div>
</div>

<!-- <div class="flex flex-wrap items-center justify-center">
  {#each $activities as activity}
    <div class="p-2">
      <ActivityViewer {activity} />
    </div>
  {/each}
</div> -->
