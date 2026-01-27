<script lang="ts">
  import type {  DailyOuraDataDTO } from "$lib/api/ApiClient";
  import { pageUpdate } from "$lib/stores/stores";

  export let dailyOuraData: DailyOuraDataDTO;

  let modalOpen = false;
  let activeTab: 'readiness' | 'sleep' | 'activity' | 'resilience' = 'readiness';
  let syncing = false;
  let syncError = "";
  let syncSuccess = false;

  function openModal() {
    modalOpen = true;
    syncError = "";
    syncSuccess = false;
  }

  function closeModal() {
    modalOpen = false;
  }

  async function syncTodayData() {
    syncing = true;
    syncError = "";
    syncSuccess = false;
    
    try {
      const today = new Date().toISOString().split('T')[0];
      const response = await fetch("/api/oura/sync", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          startDate: today,
          endDate: today,
        }),
      });

      if (response.ok) {
        syncSuccess = true;
        // Trigger page update to reload data
        pageUpdate.set(new Date());
        setTimeout(() => {
          syncSuccess = false;
        }, 3000);
      } else {
        syncError = "Failed to sync Oura data";
      }
    } catch (err) {
      syncError = "Error syncing Oura data";
      console.error(err);
    } finally {
      syncing = false;
    }
  }

  function formatDate(date: any): string {
    if (!date) return "N/A";
    const d = new Date(date.toString());
    return d.toLocaleDateString("en-US", {
      month: "short",
      day: "numeric",
      year: "numeric",
      timeZone: "UTC"
    });
  }

  function getScoreColor(score: number): string {
    if (score >= 85) return 'text-success';
    if (score >= 70) return 'text-warning';
    return 'text-error';
  }

  function getProgressColor(score: number): string {
    if (score >= 85) return 'progress-success';
    if (score >= 70) return 'progress-warning';
    return 'progress-error';
  }
</script>

<!-- Card -->
<button
  on:click={openModal}
  class="card bg-base-100 shadow-editorial hover:shadow-editorial-lg transition-all duration-200
         cursor-pointer p-6 text-left w-full h-full
         border-2 border-base-content/10 hover:border-base-content/30"
>
  <div class="flex items-start justify-between mb-6">
    <div class="flex flex-col gap-1">
      <span class="text-xs font-bold uppercase tracking-widest text-base-content/50">Metrics</span>
      <h3 class="text-2xl font-display font-black tracking-tight">Oura</h3>
    </div>
    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-6 h-6 text-base-content/30">
      <path stroke-linecap="round" stroke-linejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5" />
    </svg>
  </div>

  <!-- Three score displays -->
  <div class="grid grid-cols-3 gap-3">
    <div class="flex flex-col gap-1">
      <span class="text-xs font-bold uppercase tracking-wider text-success/70">Ready</span>
      <span class="text-4xl font-display font-black text-success leading-none">{dailyOuraData.readinessData?.readinessScore ?? 0}</span>
    </div>
    <div class="flex flex-col gap-1">
      <span class="text-xs font-bold uppercase tracking-wider text-info/70">Sleep</span>
      <span class="text-4xl font-display font-black text-info leading-none">{dailyOuraData.sleepData?.sleepScore ?? 0}</span>
    </div>
    <div class="flex flex-col gap-1">
      <span class="text-xs font-bold uppercase tracking-wider text-warning/70">Active</span>
      <span class="text-4xl font-display font-black text-warning leading-none">{dailyOuraData.activityData?.activityScore ?? 0}</span>
    </div>
  </div>
</button>

<!-- Modal -->
<dialog class="modal" class:modal-open={modalOpen}>
  <div class="modal-box rounded-3xl max-w-2xl bg-base-100 p-0 overflow-hidden max-h-[90vh]">
    <!-- Header -->
    <div class="p-6 pb-4 border-b border-base-200">
      <div class="flex items-center justify-between mb-3">
        <div class="flex items-center gap-3">
          <div class="w-12 h-12 rounded-2xl bg-accent/10 flex items-center justify-center">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-6 h-6 text-accent">
              <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
            </svg>
          </div>
          <div>
            <h3 class="font-semibold text-lg">Oura Metrics</h3>
            <p class="text-sm text-base-content/50">{formatDate(dailyOuraData.date) || 'Today'}</p>
          </div>
        </div>
        <div class="flex gap-2">
          <button
            on:click={syncTodayData}
            disabled={syncing}
            class="btn btn-ghost btn-sm btn-circle"
            title="Sync today's data"
          >
            {#if syncing}
              <span class="loading loading-spinner loading-sm"></span>
            {:else}
              <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-5 h-5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182m0-4.991v4.99" />
              </svg>
            {/if}
          </button>
          <button on:click={closeModal} class="btn btn-ghost btn-sm btn-circle">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      </div>
      
      <!-- Success/Error Messages -->
      {#if syncSuccess}
        <div class="alert alert-success rounded-xl py-2">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 shrink-0 stroke-current" fill="none" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <span class="text-sm">Data synced successfully!</span>
        </div>
      {/if}
      {#if syncError}
        <div class="alert alert-error rounded-xl py-2">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 shrink-0 stroke-current" fill="none" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <span class="text-sm">{syncError}</span>
        </div>
      {/if}
    </div>

    <!-- Tabs -->
    <div class="px-6 pt-4">
      <div class="flex gap-2 p-1 bg-base-200 rounded-2xl">
        <button
          on:click={() => activeTab = 'readiness'}
          class="flex-1 py-2 px-4 rounded-xl text-sm font-medium transition-all duration-200
                 {activeTab === 'readiness' ? 'bg-base-100 shadow-sm' : 'hover:bg-base-300'}"
        >
          Readiness
        </button>
        <button
          on:click={() => activeTab = 'sleep'}
          class="flex-1 py-2 px-4 rounded-xl text-sm font-medium transition-all duration-200
                 {activeTab === 'sleep' ? 'bg-base-100 shadow-sm' : 'hover:bg-base-300'}"
        >
          Sleep
        </button>
        <button
          on:click={() => activeTab = 'activity'}
          class="flex-1 py-2 px-4 rounded-xl text-sm font-medium transition-all duration-200
                 {activeTab === 'activity' ? 'bg-base-100 shadow-sm' : 'hover:bg-base-300'}"
        >
          Activity
        </button>
        <button
          on:click={() => activeTab = 'resilience'}
          class="flex-1 py-2 px-4 rounded-xl text-sm font-medium transition-all duration-200
                 {activeTab === 'resilience' ? 'bg-base-100 shadow-sm' : 'hover:bg-base-300'}"
        >
          Resilience
        </button>
      </div>
    </div>

    <!-- Content -->
    <div class="p-6 overflow-y-auto max-h-[60vh]">
      {#if activeTab === 'readiness'}
        <div class="text-center mb-6">
          <p class="text-6xl font-bold {getScoreColor(dailyOuraData.readinessData?.readinessScore ?? 0)}">{dailyOuraData.readinessData?.readinessScore ?? 0}</p>
          <p class="text-base-content/50 mt-1">Readiness Score</p>
        </div>
        <div class="space-y-4">
          {#each [
            { label: 'Sleep Balance', value: dailyOuraData.readinessData?.sleepBalance },
            { label: 'Activity Balance', value: dailyOuraData.readinessData?.activityBalance },
            { label: 'Recovery Index', value: dailyOuraData.readinessData?.recoveryIndex },
            { label: 'HRV Balance', value: dailyOuraData.readinessData?.hrvBalance },
            { label: 'Previous Night', value: dailyOuraData.readinessData?.previousNight },
            { label: 'Previous Day Activity', value: dailyOuraData.readinessData?.previousDayActivity },
            { label: 'Resting Heart Rate', value: dailyOuraData.readinessData?.restingHeartRate },
          ] as item}
            <div class="flex items-center justify-between">
              <span class="text-sm">{item.label}</span>
              <div class="flex items-center gap-3 w-1/2">
                <progress class="progress {getProgressColor(item.value ?? 0)} flex-1 h-2" value={item.value ?? 0} max="100"></progress>
                <span class="text-sm font-medium w-8 text-right">{item.value ?? 0}</span>
              </div>
            </div>
          {/each}
          <div class="flex items-center justify-between pt-2 border-t border-base-200">
            <span class="text-sm">Temperature Deviation</span>
            <span class="font-medium">{dailyOuraData.readinessData?.temperatureDeviation ?? 0}°</span>
          </div>
        </div>
      {:else if activeTab === 'sleep'}
        <div class="text-center mb-6">
          <p class="text-6xl font-bold {getScoreColor(dailyOuraData.sleepData?.sleepScore ?? 0)}">{dailyOuraData.sleepData?.sleepScore ?? 0}</p>
          <p class="text-base-content/50 mt-1">Sleep Score</p>
        </div>
        <div class="space-y-4">
          {#each [
            { label: 'Total Sleep', value: dailyOuraData.sleepData?.totalSleep },
            { label: 'Efficiency', value: dailyOuraData.sleepData?.efficiency },
            { label: 'Restfulness', value: dailyOuraData.sleepData?.restfulness },
            { label: 'Timing', value: dailyOuraData.sleepData?.timing },
            { label: 'Latency', value: dailyOuraData.sleepData?.latency },
            { label: 'REM Sleep', value: dailyOuraData.sleepData?.remSleep },
            { label: 'Deep Sleep', value: dailyOuraData.sleepData?.deepSleep },
          ] as item}
            <div class="flex items-center justify-between">
              <span class="text-sm">{item.label}</span>
              <div class="flex items-center gap-3 w-1/2">
                <progress class="progress {getProgressColor(item.value ?? 0)} flex-1 h-2" value={item.value ?? 0} max="100"></progress>
                <span class="text-sm font-medium w-8 text-right">{item.value ?? 0}</span>
              </div>
            </div>
          {/each}
        </div>
      {:else if activeTab === 'activity'}
        <div class="text-center mb-6">
          <p class="text-6xl font-bold {getScoreColor(dailyOuraData.activityData?.activityScore ?? 0)}">{dailyOuraData.activityData?.activityScore ?? 0}</p>
          <p class="text-base-content/50 mt-1">Activity Score</p>
        </div>

        <div class="grid grid-cols-3 gap-4 mb-6">
          <div class="bg-base-200 rounded-2xl p-4 text-center">
            <p class="text-2xl font-bold">{dailyOuraData.activityData?.steps?.toLocaleString()}</p>
            <p class="text-xs text-base-content/50">Steps</p>
          </div>
          <div class="bg-base-200 rounded-2xl p-4 text-center">
            <p class="text-2xl font-bold">{dailyOuraData.activityData?.activeCalories}</p>
            <p class="text-xs text-base-content/50">Active Cal</p>
          </div>
          <div class="bg-base-200 rounded-2xl p-4 text-center">
            <p class="text-2xl font-bold">{dailyOuraData.activityData?.totalCalories}</p>
            <p class="text-xs text-base-content/50">Total Cal</p>
          </div>
        </div>

        <div class="space-y-4">
          {#each [
            { label: 'Training Frequency', value: dailyOuraData.activityData?.trainingFrequency },
            { label: 'Training Volume', value: dailyOuraData.activityData?.trainingVolume },
            { label: 'Recovery Time', value: dailyOuraData.activityData?.recoveryTime },
            { label: 'Stay Active', value: dailyOuraData.activityData?.stayActive },
            { label: 'Move Every Hour', value: dailyOuraData.activityData?.moveEveryHour },
            { label: 'Meet Daily Targets', value: dailyOuraData.activityData?.meetDailyTargets },
          ] as item}
            <div class="flex items-center justify-between">
              <span class="text-sm">{item.label}</span>
              <div class="flex items-center gap-3 w-1/2">
                <progress class="progress {getProgressColor(item.value ?? 0)} flex-1 h-2" value={item.value ?? 0} max="100"></progress>
                <span class="text-sm font-medium w-8 text-right">{item.value ?? 0}</span>
              </div>
            </div>
          {/each}
        </div>
      {:else if activeTab === 'resilience'}
        <div class="text-center mb-6">
          <p class="text-4xl font-bold text-accent">{dailyOuraData.resilienceData?.resilienceLevel ?? 0}</p>
          <p class="text-base-content/50 mt-1">Resilience Level</p>
        </div>
        <div class="space-y-4">
          {#each [
            { label: 'Sleep Recovery', value: dailyOuraData.resilienceData?.sleepRecovery },
            { label: 'Daytime Recovery', value: dailyOuraData.resilienceData?.daytimeRecovery },
            { label: 'Stress', value: dailyOuraData.resilienceData?.stress },
          ] as item}
            <div class="flex items-center justify-between">
              <span class="text-sm">{item.label}</span>
              <div class="flex items-center gap-3 w-1/2">
                <progress class="progress {getProgressColor(item.value ?? 0)} flex-1 h-2" value={item.value ?? 0} max="100"></progress>
                <span class="text-sm font-medium w-8 text-right">{item.value ?? 0}</span>
              </div>
            </div>
          {/each}
        </div>
      {/if}
    </div>
  </div>
  <form method="dialog" class="modal-backdrop bg-black/50 backdrop-blur-sm">
    <button on:click={closeModal}>close</button>
  </form>
</dialog>
