<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import { bootUserDto, fetchBootUserDto, pageUpdate } from "$lib/stores/stores";
  import {
    DailyOuraDataDTO,
    GetDailyOuraDataEndpointClient,
    GetWellnessStateEndpointClient,
    WellnessState,
  } from "$lib/api/ApiClient";
  import { training } from "$lib/stores/training.svelte";

  import WellnessCard from "$lib/components/home/WellnessCard.svelte";
  import OuraCard from "$lib/components/home/OuraCard.svelte";
  import NextSessionCard from "$lib/components/home/NextSessionCard.svelte";
  import LastSessionCard from "$lib/components/home/LastSessionCard.svelte";
  import InjuryOverviewCard from "$lib/components/home/InjuryOverviewCard.svelte";
  import ChatPreviewCard from "$lib/components/home/ChatPreviewCard.svelte";

  const baseUrl = "";
  let selectedDate = $state(new Date().toDateString());

  let wellnessState = $state<WellnessState>(new WellnessState());
  let dailyOuraInfo = $state<DailyOuraDataDTO>(new DailyOuraDataDTO());

  // Other pages bump `pageUpdate` after a write; refetch when it moves on.
  let lastHandledUpdate = $pageUpdate;

  onMount(async () => {
    await fetchBootUserDto(fetch);
    if ($bootUserDto.name === null || !$bootUserDto.hasCreatedProfile) {
      goto("/auth");
      return;
    }
    await refresh();
  });

  async function refresh() {
    await Promise.all([training.loadAll(), fetchWellnessState(), loadDailyOuraInfo()]);
  }

  async function fetchWellnessState() {
    try {
      wellnessState = await new GetWellnessStateEndpointClient(baseUrl).get(new Date(selectedDate));
    } catch (error) {
      console.error("Error fetching wellness state:", error);
    }
  }

  async function loadDailyOuraInfo() {
    try {
      dailyOuraInfo = await new GetDailyOuraDataEndpointClient(baseUrl).get(new Date(selectedDate));
    } catch (error) {
      console.error("Error loading daily oura info:", error);
    }
  }

  $effect(() => {
    if ($pageUpdate === lastHandledUpdate) return;
    lastHandledUpdate = $pageUpdate;
    refresh();
  });
</script>

<svelte:head>
  <title>Lionheart - Home</title>
</svelte:head>

<div class="min-h-full bg-base-200">
  <div class="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
    <header class="mb-8">
      <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1
            class="font-display text-5xl font-black leading-none tracking-tightest text-base-content sm:text-6xl"
          >
            {#if $bootUserDto?.name}
              {$bootUserDto.name.toUpperCase()}
            {:else}
              LIONHEART
            {/if}
          </h1>
          <p class="mt-3 font-mono text-sm uppercase tracking-widest text-base-content/50">
            {selectedDate}
          </p>
        </div>

        <div class="flex items-center gap-2">
          <input
            type="date"
            bind:value={selectedDate}
            onchange={refresh}
            class="input input-sm min-w-[140px] border-2 border-base-content/10 bg-base-100 text-center
                   font-mono text-xs uppercase tracking-widest focus:border-base-content/30 focus:outline-hidden"
          />

          <button
            onclick={() => {
              selectedDate = new Date().toDateString();
              refresh();
            }}
            class="btn btn-ghost btn-sm border-2 border-base-content/10 px-4"
            class:hidden={selectedDate === new Date().toDateString()}
          >
            Today
          </button>
        </div>
      </div>
    </header>

    <a
      href="/log"
      class="mb-8 flex items-center justify-between rounded-2xl border-2 border-base-content bg-base-content
             px-6 py-5 text-base-100 shadow-editorial-lg transition-transform active:scale-[0.99]"
    >
      <span>
        <span class="block font-display text-2xl font-black tracking-tight">Log a session</span>
        <span class="mt-1 block text-xs font-bold uppercase tracking-widest opacity-70">
          Say it, and it gets written down
        </span>
      </span>
      <span class="text-2xl">&rarr;</span>
    </a>

    <div class="mb-8 grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
      <WellnessCard
        wellnessState={{
          motivationScore: wellnessState.motivationScore ?? 0,
          stressScore: wellnessState.stressScore ?? 0,
          moodScore: wellnessState.moodScore ?? 0,
          energyScore: wellnessState.energyScore ?? 0,
          overallScore: wellnessState.overallScore ?? 0,
          date: wellnessState.date?.toString() ?? selectedDate.toString(),
        }}
      />

      <OuraCard dailyOuraData={dailyOuraInfo} />

      <LastSessionCard session={training.lastCompletedSession} />

      <NextSessionCard session={training.nextSession} />

      <InjuryOverviewCard {baseUrl} />

      <div class="md:col-span-2 lg:col-span-3">
        <ChatPreviewCard />
      </div>
    </div>
  </div>
</div>
