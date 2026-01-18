<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import {
    bootUserDto,
    fetchBootUserDto,
    pageUpdate,
  } from "$lib/stores/stores.js";
  import { writable } from "svelte/store";


  import {
    ActivityDTO,
    DailyOuraDataDTO,
    GetActivitiesEndpointClient,
    GetDailyOuraDataEndpointClient,
    GetTrainingProgramsEndpointClient,
    GetWellnessStateEndpointClient,
    TrainingProgramDTO,
    TrainingSessionDTO,
    TrainingSessionStatus,
    WellnessState,
  } from "$lib/api/ApiClient";

  // Import card components
  import WellnessCard from "$lib/components/home/WellnessCard.svelte";
  import OuraCard from "$lib/components/home/OuraCard.svelte";
  import NextSessionCard from "$lib/components/home/NextSessionCard.svelte";
  import LastSessionCard from "$lib/components/home/LastSessionCard.svelte";
  import LastActivityCard from "$lib/components/home/LastActivityCard.svelte";
  import InjuryOverviewCard from "$lib/components/home/InjuryOverviewCard.svelte";
    import { theme } from "$lib/stores/themeStore";



  const baseUrl = "";
  let lastUpdatePage = $pageUpdate;
  let selectedDate = new Date().toISOString().slice(0, 10);

  // Create stores
  const wellnessState = writable<WellnessState>(new WellnessState());
  const dailyOuraInfo = writable<DailyOuraDataDTO>(new DailyOuraDataDTO());
  const activities = writable<ActivityDTO[]>([]);

  let sessions: TrainingSessionDTO[] = [];
  let lastCompletedSession: TrainingSessionDTO | null = null;
  let nextUpcomingSession: TrainingSessionDTO | null = null;
  let programs: TrainingProgramDTO[] = [];

  onMount(async () => {
    await fetchBootUserDto(fetch);
    if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
      goto("/auth");
    } else {
      await Promise.all([
        fetchWellnessState(),
        loadSessions(),
        loadPrograms(),
        loadActivities(),
        loadDailyOuraInfo()
      ]);
    }
  });

  async function fetchWellnessState() {
    try {
      const client = new GetWellnessStateEndpointClient(baseUrl);
      const data = await client.get(new Date(selectedDate));
      wellnessState.set(data);
    } catch (error) {
      console.error("Error fetching wellness state:", error);
    }
  }

  async function updatePageInfo() {
    await Promise.all([
      fetchWellnessState(),
      loadActivities(),
      loadDailyOuraInfo()
    ]);
  }

  $: {
    if (lastUpdatePage != $pageUpdate) {
      updatePageInfo();
    }
  }

  function computeOverview() {
    const done = sessions.filter((s) => s.status === TrainingSessionStatus._2);
    lastCompletedSession = done.length ? done[done.length - 1] : null;
    nextUpcomingSession =
      sessions.find(
        (s) =>
          s.status === undefined ||
          Number(s.status) === Number(TrainingSessionStatus._0),
      ) ?? null;
  }

  async function loadSessions() {
    const client = new GetTrainingProgramsEndpointClient(baseUrl);
    try {
      const all = await client.get();
      if (all.length === 0) return;
      sessions = (all[0].trainingSessions ?? [])
        .slice()
        .sort(
          (a, b) =>
            new Date(a.date ?? "").getTime() - new Date(b.date ?? "").getTime(),
        );
      computeOverview();
    } catch (e) {
      console.error("loadSessions failed", e);
    }
  }

  async function loadPrograms() {
    const client = new GetTrainingProgramsEndpointClient(baseUrl);
    try {
      programs = await client.get();
    } catch (e) {
      console.error("loadPrograms failed", e);
    }
  }

  


  async function loadActivities() {
    try {
      const client = new GetActivitiesEndpointClient(baseUrl);
      const start = new Date(selectedDate);
      const end = new Date(selectedDate);
      const data = await client.get(start, end);
      activities.set(data ?? []);
    } catch (error) {
      console.error("Error loading activities:", error);
      activities.set([]);
    }
  }

  async function loadDailyOuraInfo() {
    try {
      const client = new GetDailyOuraDataEndpointClient(baseUrl);
      const data = await client.get(new Date(selectedDate));
      dailyOuraInfo.set(data);
    } catch (error) {
      console.error("Error loading daily oura info:", error);
    }
  }
</script>

<svelte:head>
  <title>Lionheart - Home</title>
</svelte:head>

<div class="min-h-full bg-base-200">
  <div class="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
    <!-- Header -->
    <header class="mb-8">
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 class="text-5xl sm:text-6xl font-display font-black tracking-tightest text-base-content leading-none">
            {#if $bootUserDto?.name}
              {$bootUserDto.name.toUpperCase()}
            {:else}
              LIONHEART
            {/if}
          </h1>
          <p class="text-sm font-mono uppercase tracking-widest text-base-content/50 mt-3">{selectedDate}</p>
        </div>

        <!-- Date Picker -->
        <div class="flex items-center gap-2">
          

          <div class="relative">
            <input
              type="date"
              bind:value={selectedDate}
              on:change={updatePageInfo}
              class="input input-sm bg-base-100 text-center min-w-[140px] border-2 border-base-content/10
                     focus:outline-none focus:border-base-content/30 font-mono uppercase tracking-widest text-xs"
            />
          </div>

        

          <button
            on:click={() => {
              selectedDate = new Date().toDateString();
              updatePageInfo();
            }}
            class="btn btn-ghost btn-sm text-xs font-bold uppercase tracking-wider border-2 border-base-content/10"
            class:hidden={selectedDate === new Date().toDateString()}
          >
            Today
          </button>
        </div>
      </div>
    </header>
    

    <!-- Card Grid -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 mb-8">
      <!-- Wellness Card -->
      <WellnessCard wellnessState={{
        motivationScore: $wellnessState.motivationScore ?? 0,
        stressScore: $wellnessState.stressScore ?? 0,
        moodScore: $wellnessState.moodScore ?? 0,
        energyScore: $wellnessState.energyScore ?? 0,
        overallScore: $wellnessState.overallScore ?? 0,
        date: $wellnessState.date?.toString() ?? selectedDate.toString()
      }} />

      <!-- Oura Card -->
      <OuraCard dailyOuraData={$dailyOuraInfo} />

      <!-- Next Session Card -->
      <NextSessionCard session={nextUpcomingSession} />

      <!-- Last Session Card -->
      <LastSessionCard session={lastCompletedSession} />

      <!-- Last Activity Card -->
      <LastActivityCard activities={$activities} />

      <!-- Injury Overview Card -->
      <InjuryOverviewCard {baseUrl} />
    </div>

    
  </div>
</div>
