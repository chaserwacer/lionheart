<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import {
    bootUserDto,
    fetchBootUserDto,
    type WellnessState,
  } from "$lib/stores";

  import ActivityTracker from "$lib/ActivityTracker.svelte";
  import WellnessTracker from "$lib/WellnessTracker.svelte";
  import { writable } from "svelte/store";

  let selectedDate = new Date().toISOString().slice(0, 10);
  const wellnessState = writable({
    motivationScore: 0,
    stressScore: 0,
    moodScore: 0,
    energyScore: 0,
    overallScore: -1,
    date: "",
  });

  onMount(async () => {
    await fetchBootUserDto(fetch);
    if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
      goto("/auth");
    }
    await fetchWellnessState();
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
  const activities = writable([]);

  async function fetchActivities() {
    try {
      const url = "api/activity/getactivities?start=" + selectedDate + "&end=" + selectedDate;
      const response = await fetch(url);

      if (response.ok) {
        const data = await response.json();
        activities.set(data);
      } else {
        console.error("Failed to get todays activities");
      }
    } catch {
      console.error("Error fetching todays activities");
    }
  }

  function updatePageInfo() {
    console.log("Update page info on data: ", selectedDate)
    fetchWellnessState();
    fetchActivities();
  }
</script>

<svelte:head>
  <title>homebase</title>
</svelte:head>

<div class="flex flex-row">
  <article class="prose max-w-none w-1/2 pl-5 pt-5">
    <h2 class="">
      Welcome, {$bootUserDto.name}, to Project Lionheart.
    </h2>
    <p>
      This page will be your one-stop-shop viewing location for all of your
      training data. Select a date to view all associated data. Track activities
      & your wellness state below | Navigating to the page for a given category
      will allow you to view data in more depth, as well as view recent trends
      and patterns.
    </p>
    <div class="flex flex-row">
      <button class=""><ActivityTracker on:update={updatePageInfo} /></button>
      <button class="pl-2"
        ><WellnessTracker on:update={updatePageInfo} /></button
      >
    </div>
  </article>
  <div class="divider divider-horizontal"></div>
  <div class="w-1/2">
    <div class="card items-center p-0 m-0">
      <div class="card-body items-center">
        <h2 class="card-title">Selected Date</h2>
        <input
          type="date"
          bind:value={selectedDate}
          on:change={updatePageInfo}
          class="btn border-primary"
        />
        <div class="divider p-0 m-0"></div>
        <!-- <div
          class="radial-progress items-center text-center p-0 m-0"
          style="--value:{monthProgress}; --size:6rem; --thickness: 1px;"
          role="progressbar"
        >
          <p class="text-xl">{month} <br />{dateNumber} / {daysInMonth}</p>
        </div> -->
      </div>
    </div>
  </div>
</div>

<div class="divider"></div>

<div class="flex md:flex-row flex-col">
  <div
    class=" ml-5 mr-5 stats stats-vertical md:stats-horizontal shadow bg-primary text-primary-content flex-initial w-full {$wellnessState.overallScore ===
    -1
      ? 'blur-sm'
      : ''}"
  >
    <div class="stat place-items-center text-center">
      <div class="stat-title text-2xl text-primary-content">
        Wellness State <br /> Overall Score
      </div>
      <div class="stat-value text-primary-content">
        {$wellnessState.overallScore} / 5
      </div>
    </div>
    <div class="stat place-items-center">
      <div
        class="radial-progress text-primary-content"
        style="--value:{($wellnessState.energyScore / 5) *
          100}; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title text-primary-content">Energy</div>
        <div class="stat-value text-primary-content">
          {$wellnessState.energyScore} / 5
        </div>
        <div class="stat-desc text-primary-content">énergie</div>
      </div>
    </div>

    <div class="stat place-items-center">
      <div
        class="radial-progress text-primary-content"
        style="--value:{($wellnessState.motivationScore / 5) *
          100}; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title text-primary-content">Motivation</div>
        <div class="stat-value text-primary-content">
          {$wellnessState.motivationScore} / 5
        </div>
        <div class="stat-desc text-primary-content">motif</div>
      </div>
    </div>

    <div class="stat place-items-center">
      <div
        class="radial-progress text-primary-content"
        style="--value:{($wellnessState.moodScore / 5) *
          100}; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title text-primary-content">Mood</div>
        <div class="stat-value text-primary-content">
          {$wellnessState.moodScore} / 5
        </div>
        <div class="stat-desc text-primary-content">humeur</div>
      </div>
    </div>
    <div class="stat place-items-center">
      <div
        class="radial-progress text-primary-content"
        style="--value:{($wellnessState.stressScore / 5) *
          100}; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title text-primary-content">Stress</div>
        <div class="stat-value text-primary-content">
          {$wellnessState.stressScore} / 5
        </div>
        <div class="stat-desc text-primary-content">soulignez</div>
      </div>
    </div>
  </div>
</div>
