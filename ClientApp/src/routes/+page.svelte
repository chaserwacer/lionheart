<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import { bootUserDto, fetchBootUserDto } from "$lib/stores";
  import { todaysWellnessState, fetchTodaysWellnessState } from "$lib/stores";
    import ActivityTracker from "$lib/ActivityTracker.svelte";
    import WellnessTracker from "$lib/WellnessTracker.svelte";
  // import { Datepicker } from 'svelte-calendar';

  let selectedDate: Date;
  let dateNumber: number;
  let month: string;
  let daysInMonth: number;
  let monthProgress: number;
  let user: { name: string; hasCreatedProfile?: boolean };

  onMount(async () => {
    await fetchBootUserDto(fetch);
    if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
      goto("/auth");
    }
    await fetchTodaysWellnessState(fetch);
    var selected = new Date();
    generateDateStuffs(selected);
  });

  function generateDateStuffs(selected: Date) {
    selectedDate = selected;
    dateNumber = selected.getDate();
    var year = selected.getFullYear();
    var tmonth = new Date().getMonth();
    month = new Date().toLocaleString("default", { month: "long" });
    daysInMonth = new Date(year, tmonth + 1, 0).getDate();
    monthProgress = (dateNumber / daysInMonth) * 100;
  }
</script>

<svelte:head>
  <title>homebase</title>
</svelte:head>
<!-- <Datepicker /> -->
<div class="flex flex-row">
  <article class="prose max-w-none w-1/2 pl-5 pt-5">
    <h2 class="">
      Welcome, {$bootUserDto.name}, to Project Lionheart.
    </h2>
    <p>
      This page will be your one-stop-shop viewing location for all of your
      training data. Select a date to view all associated data.
    </p>
    <div class=""><ActivityTracker/></div>
    <div class="pt-2"><WellnessTracker/></div>
  </article>
  <div class="divider divider-horizontal"></div>
  <div class="w-1/2">
    <div class="card items-center p-0 m-0">
      <div class="card-body items-center">
        <h2 class="card-title">Selected Date</h2>
        <input type="date" bind:value={selectedDate} class="btn border-primary" />
        <div class="divider p-0 m-0"></div>
        <div
          class="radial-progress items-center text-center p-0 m-0"
          style="--value:{monthProgress}; --size:6rem; --thickness: 1px;"
          role="progressbar"
        >
          <p class="text-xl">{month} <br />{dateNumber} / {daysInMonth}</p>

          
        </div>
        
      </div>
    </div>
  </div>
</div>

<div class="divider"></div>

<div class="flex md:flex-row flex-col">
  <div
    class=" ml-5 mr-5 stats stats-vertical md:stats-horizontal shadow bg-primary text-primary-content flex-initial w-full {$todaysWellnessState.overallScore ===
    -1
      ? 'blur-sm'
      : ''}"
  >
    <div class="stat place-items-center text-center">
      <div class="stat-title text-2xl text-primary-content">
        Wellness State <br /> Overall Score
      </div>
      <div class="stat-value text-primary-content">
        {$todaysWellnessState.overallScore} / 5
      </div>
    </div>
    <div class="stat place-items-center">
      <div
        class="radial-progress text-primary-content"
        style="--value:{($todaysWellnessState.energyScore / 5) *
          100}; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title text-primary-content">Energy</div>
        <div class="stat-value text-primary-content">
          {$todaysWellnessState.energyScore} / 5
        </div>
        <div class="stat-desc text-primary-content">énergie</div>
      </div>
    </div>

    <div class="stat place-items-center">
      <div
        class="radial-progress text-primary-content"
        style="--value:{($todaysWellnessState.motivationScore / 5) *
          100}; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title text-primary-content">Motivation</div>
        <div class="stat-value text-primary-content">
          {$todaysWellnessState.motivationScore} / 5
        </div>
        <div class="stat-desc text-primary-content">motif</div>
      </div>
    </div>

    <div class="stat place-items-center">
      <div
        class="radial-progress text-primary-content"
        style="--value:{($todaysWellnessState.moodScore / 5) *
          100}; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title text-primary-content">Mood</div>
        <div class="stat-value text-primary-content">
          {$todaysWellnessState.moodScore} / 5
        </div>
        <div class="stat-desc text-primary-content">humeur</div>
      </div>
    </div>
    <div class="stat place-items-center">
      <div
        class="radial-progress text-primary-content"
        style="--value:{($todaysWellnessState.stressScore / 5) *
          100}; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title text-primary-content">Stress</div>
        <div class="stat-value text-primary-content">
          {$todaysWellnessState.stressScore} / 5
        </div>
        <div class="stat-desc text-primary-content">soulignez</div>
      </div>
    </div>
  </div>
</div>
