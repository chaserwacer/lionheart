<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import { bootUserDto, fetchBootUserDto } from "$lib/stores";
  import { todaysWellnessState, fetchTodaysWellnessState } from "$lib/stores";

  let currentDate: number;
  let month: string
  let daysInMonth: number;
  let monthProgress: number;

  onMount(async () => {
    await fetchBootUserDto(fetch);
    if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
      goto("/auth");
    }
    await fetchTodaysWellnessState(fetch)
    currentDate = new Date().getDate();
    var year = new Date().getFullYear();
    var tmonth = new Date().getMonth();
    month = new Date().toLocaleString('default', { month: 'long' });
    daysInMonth = new Date(year, tmonth + 1, 0).getDate();
    monthProgress = (currentDate / daysInMonth) * 100;
  });
</script>

<svelte:head>
  <title>homebase</title>
</svelte:head>

<div>
  <article class="prose max-w-none p-5">
    <h2 class="">
      Welcome, {$bootUserDto.name}, to Project Lionheart. Below is your Homebase
      / Overview Page
    </h2>
    <p>
      This will be your one-stop-shop viewing location for all of your training
      data. Navigate to each necessary page to add data.
    </p>
  </article>
</div>
<div class="divider"></div>
<div class="flex md:flex-row flex-col">
  <div class="md:w-1/4 w-full">
    <div class="card bg-primary-content shadow-xl items-center">
      <div class="card-body items-center">
        <h2 class="card-title">Today's Date + Wellness State</h2>
        <div class="divider p-0 m-0"></div>
        <div
          class="radial-progress items-center text-center"
          style="--value:{monthProgress}; --size:12rem; --thickness: 1px;"
          role="progressbar"
        >
          <p class="text-4xl">{month} <br>{currentDate} / {daysInMonth}</p>
        </div>
      </div>
    </div>
  </div>
  
  <div
    class="stats stats-vertical md:stats-horizontal shadow bg-secondary text-secondary-content flex-initial md:w-3/4 w-full {$todaysWellnessState.overallScore ===
    -1
      ? 'blur-sm'
      : ''}"
  >
    <div class="stat place-items-center">
      <div
        class="radial-progress"
        style="--value:{($todaysWellnessState.energyScore / 5) *
          100}; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title">Energy</div>
        <div class="stat-value">{$todaysWellnessState.energyScore} / 5</div>
        <div class="stat-desc">énergie</div>
      </div>
    </div>

    <div class="stat place-items-center">
      <div
        class="radial-progress"
        style="--value:{($todaysWellnessState.motivationScore / 5) *
          100}; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title">Motivation</div>
        <div class="stat-value">{$todaysWellnessState.motivationScore} / 5</div>
        <div class="stat-desc">motif</div>
      </div>
    </div>

    <div class="stat place-items-center">
      <div
        class="radial-progress"
        style="--value:{($todaysWellnessState.moodScore / 5) *
          100}; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title">Mood</div>
        <div class="stat-value">{$todaysWellnessState.moodScore} / 5</div>
        <div class="stat-desc">humeur</div>
      </div>
    </div>
    <div class="stat place-items-center">
      <div
        class="radial-progress"
        style="--value:{($todaysWellnessState.stressScore / 5) *
          100}; --size:8rem; --thickness: 2px;"
        role="progressbar"
      >
        <div class="stat-title">Stress</div>
        <div class="stat-value">{$todaysWellnessState.stressScore} / 5</div>
        <div class="stat-desc">soulignez</div>
      </div>
    </div>
  </div>
</div>
