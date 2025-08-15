<script lang="ts">
  import { createEventDispatcher, onMount } from "svelte";
  import { browser } from "$app/environment";
  import {
    CreateTrainingProgramEndpointClient,
    CreateTrainingProgramRequest,
    GenerateProgramPreferencesEndpointClient,
    GenerateProgramFirstWeekEndpointClient,
    GenerateProgramRemainingWeeksEndpointClient,
    FirstWeekGenerationDTO,
    RemainingWeeksGenerationDTO,
  } from "$lib/api/ApiClient";

  export let show: boolean;
  const dispatch = createEventDispatcher();

  // ---------- Base state ----------
  let title = '';
  let startDate = '';
  let endDate = '';
  let selectedTag = 'Powerlifting';
  const tagOptions = ['Powerlifting', 'Bodybuilding'];

  const baseUrl = browser ? window.location.origin : "http://localhost:5174";
  let plainClient: CreateTrainingProgramEndpointClient | null = null;

  let prefClient: GenerateProgramPreferencesEndpointClient | null = null;
  let week1Client: GenerateProgramFirstWeekEndpointClient | null = null;
  let weekXClient: GenerateProgramRemainingWeeksEndpointClient | null = null;

  let aiStep = 0; // 0=create, 1=prefs, 2=outline, 3=week1, 4+=cont
  let aiResponse: string | null = null;
  let isAiLoading = false;

  // ---------- Shared preference fields ----------
  let daysPerWeek = "4";
  let preferredDays = "Mon, Wed, Fri";
  let favoriteMovements = "";
  let userGoals = '';

  // ---------- Powerlifting-only fields ----------
  let squatDays = "2";
  let benchDays = "3";
  let deadliftDays = "1";

  // ---------- Bodybuilding-only fields ----------
  let weakPointsInput = "";      // comma-separated or multiline
  let bodyweight = "";           // allow "172 lbs" or "78 kg"
  let yearsOfExperience = "";    // number as string input

  // ---------- AI results ----------
  type OutlineDay = {
    Day: string;
    Focus: string;
    MainLifts: string[];
    Accessories: string[];
  };

  type ProgramOutlineDTO = {
    Summary: string;
    Microcycle: OutlineDay[];
    AccessoryHighlights: string[];
  };

  let outline: ProgramOutlineDTO | null = null;
  let redoFeedback = "";

  // ---------- Program / flow flags ----------
  let trainingProgramID = '';
  let preferencesSubmitted = false;
  let week1Generated = false;
  let remainingWeeksGenerated = false;

  onMount(() => {
    if (browser) {
      plainClient = new CreateTrainingProgramEndpointClient(baseUrl);
      prefClient = new GenerateProgramPreferencesEndpointClient(baseUrl);
      week1Client = new GenerateProgramFirstWeekEndpointClient(baseUrl);
      weekXClient = new GenerateProgramRemainingWeeksEndpointClient(baseUrl);
    }
  });

  function close() {
    dispatch('close');
  }

  function safeParseJson(raw: unknown): any | null {
    if (raw == null) return null;
    if (typeof raw !== "string") return null;
    const t = raw.trim();
    if (!(t.startsWith("{") || t.startsWith("["))) return null;
    try { return JSON.parse(t); } catch { return null; }
  }

  function reset() {
    title = '';
    startDate = '';
    endDate = '';
    selectedTag = 'Powerlifting';
    aiStep = 0;
    aiResponse = null;
    trainingProgramID = '';
    outline = null;
    preferencesSubmitted = false;
    week1Generated = false;
    remainingWeeksGenerated = false;
  }

  // ---------- Helpers ----------
  function parseWeakPoints(input: string): string[] {
    // supports comma or newline separation
    return input
      .split(/,|\n/g)
      .map(s => s.trim())
      .filter(Boolean);
  }

  function getSportHelp(tag: string): string {
    if (tag === 'Powerlifting') {
      return 'We’ll balance Squat/Bench/Deadlift frequencies across your preferred days.';
    }
    if (tag === 'Bodybuilding') {
      return 'We’ll choose a split that fits your week (e.g., Upper/Lower x2 or PPL) and bias weak points.';
    }
    return 'Tell us your availability and goals — we’ll adapt the plan.';
  }

  function buildPreferencesPayload(withUserAdjustments: string | null = null) {
    const base = {
      daysPerWeek: Number(daysPerWeek) || 0,
      preferredDays: preferredDays?.trim() || "",
      favoriteMovements: favoriteMovements?.trim() || "",
      userGoals: (() => {
        const core = userGoals?.trim() || "";
        return withUserAdjustments
          ? (core ? `${core}\n\n${withUserAdjustments}` : withUserAdjustments)
          : core;
      })()
    } as Record<string, unknown>;

    // Sport-specific fields (top-level). Server captures unknowns into Extra via [JsonExtensionData].
    if (selectedTag === 'Powerlifting') {
      base["squatDays"]   = Number(squatDays) || 0;
      base["benchDays"]   = Number(benchDays) || 0;
      base["deadliftDays"]= Number(deadliftDays) || 0;
    } else if (selectedTag === 'Bodybuilding') {
      base["weakPoints"] = parseWeakPoints(weakPointsInput);
      if (bodyweight?.trim()) base["bodyweight"] = bodyweight.trim();
      const yoe = Number(yearsOfExperience);
      if (!Number.isNaN(yoe) && yoe > 0) base["yearsOfExperience"] = yoe;
    }

    return base;
  }

  // ---------- Actions ----------
  async function createProgram() {
    if (!title || !startDate || !endDate || !selectedTag) {
      alert("All fields are required.");
      return;
    }
    if (!plainClient) {
      alert("API client not initialized.");
      return;
    }

    const addDays = (date: string | Date, days: number): Date => {
      const d = new Date(date);
      d.setDate(d.getDate() + days);
      return d;
    };

    const request = CreateTrainingProgramRequest.fromJS({
      title,
      startDate: addDays(startDate, 1).toISOString().split("T")[0],
      endDate: addDays(endDate, 1).toISOString().split("T")[0],
      tags: [selectedTag],
    });

    try {
      await plainClient.create6(request);
      reset();
      dispatch("created");
    } catch (error) {
      console.error("Failed to create program:", error);
      alert("There was an error creating the program.");
    }
  }

  async function createWithAi() {
    if (!plainClient) {
      alert('API client not initialized.');
      return;
    }
    if (!title || !startDate || !endDate || !selectedTag) {
      alert('All fields are required.');
      return;
    }

    aiStep = 1;
    isAiLoading = true;
    aiResponse = null;

    try {
      const request = CreateTrainingProgramRequest.fromJS({
        title: title.trim(),
        startDate: new Date(startDate).toISOString().split('T')[0],
        endDate: new Date(endDate).toISOString().split('T')[0],
        tags: [selectedTag]
      });
      const result = await plainClient.create6(request);
      trainingProgramID = result.trainingProgramID;
    } catch (error) {
      console.error('AI program creation error:', error);
      alert('There was an error creating the program.');
      aiStep = 0;
    } finally {
      isAiLoading = false;
    }
  }

  async function sendPreferences() {
    if (!trainingProgramID) return;

    isAiLoading = true;
    aiResponse = null;
    outline = null;

    try {
      const payload = buildPreferencesPayload();

      const res = await fetch(`${baseUrl}/api/ai/program/preferences`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Program-Tag": selectedTag,
          "X-Program-Id": trainingProgramID,
        },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        const errText = await res.text().catch(() => "");
        throw new Error(`HTTP ${res.status} ${res.statusText} ${errText}`);
      }

      const text = await res.text();
      const parsed = safeParseJson(text);
      if (parsed) {
        outline = parsed as ProgramOutlineDTO;
        aiStep = 2;
      } else {
        aiResponse = text; // show raw if non-JSON
        aiStep = 2;
      }
    } catch (err) {
      console.error("Preferences error:", err);
      aiResponse = "Error sending preferences.";
      aiStep = 1;
    } finally {
      isAiLoading = false;
      preferencesSubmitted = true;
    }
  }

  async function redoOutline() {
    if (!trainingProgramID) return;
    if (!redoFeedback.trim()) return;

    isAiLoading = true;
    aiResponse = null;

    try {
      const payload = buildPreferencesPayload(`User Adjustments: ${redoFeedback.trim()}`);

      const res = await fetch(`${baseUrl}/api/ai/program/preferences`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Program-Tag": selectedTag,
          "X-Program-Id": trainingProgramID,
        },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        const errText = await res.text().catch(() => "");
        throw new Error(`HTTP ${res.status} ${res.statusText} ${errText}`);
      }

      const text = await res.text();
      const parsed = safeParseJson(text);
      if (parsed) {
        outline = parsed as ProgramOutlineDTO;
        aiResponse = null;
      } else {
        outline = null;
        aiResponse = text;
      }

      aiStep = 2;
      redoFeedback = "";
    } catch (err) {
      console.error("Redo outline error:", err);
      aiResponse = "Error re-generating outline.";
    } finally {
      isAiLoading = false;
    }
  }

  async function generateFirstWeek() {
    if (!week1Client || !trainingProgramID) return;

    isAiLoading = true;
    aiResponse = null;

    try {
      const firstDto = FirstWeekGenerationDTO.fromJS({ trainingProgramID });
      aiResponse = await week1Client.firstWeek(firstDto);
      aiStep = 3;
    } catch (err) {
      console.error('AI week1 error:', err);
      aiResponse = 'Error generating first week.';
      aiStep = 2;
    } finally {
      isAiLoading = false;
      week1Generated = true;
    }
  }

  async function generateNextWeek() {
    if (!weekXClient) return;

    isAiLoading = true;
    aiResponse = null;

    try {
      const weekDto = RemainingWeeksGenerationDTO.fromJS({ trainingProgramID });
      aiResponse = await weekXClient.continueWeeks(weekDto);
      aiStep += 1;
    } catch (err) {
      console.error('AI weekX error:', err);
      aiResponse = 'Error generating additional weeks.';
      aiStep -= 1;
    } finally {
      isAiLoading = false;
      remainingWeeksGenerated = true;
    }
  }
</script>

{#if show}
  <div class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center">
    <div class="bg-base-200 text-base-content rounded-xl w-full max-w-2xl border border-base-300 max-h-[90vh] flex flex-col shadow-2xl overflow-hidden">

      <!-- Header -->
      <div class="p-5 border-b border-base-300 flex items-center justify-between bg-base-100">
        <div class="flex items-center gap-3">
          <h2 class="text-2xl font-bold">Create New Program</h2>
          <span class="badge badge-primary">{selectedTag}</span>
        </div>
        <button on:click={close} class="btn btn-ghost btn-sm">Close</button>
      </div>

      <!-- Stepper -->
      <div class="px-5 pt-4">
        <ul class="steps w-full">
          <li class={"step " + (aiStep >= 0 ? "step-primary" : "")}>Details</li>
          <li class={"step " + (aiStep >= 1 ? "step-primary" : "")}>Preferences</li>
          <li class={"step " + (aiStep >= 2 ? "step-primary" : "")}>Outline</li>
          <li class={"step " + (aiStep >= 3 ? "step-primary" : "")}>Week 1</li>
          <li class={"step " + (aiStep >= 4 ? "step-primary" : "")}>Weeks 2–3</li>
        </ul>
      </div>

      <!-- Scrollable Body -->
      <div class="p-5 overflow-y-auto space-y-6" style="max-height: calc(90vh - 11rem);">

        <!-- Phase 0: Initial Inputs -->
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div class="sm:col-span-2">
            <label class="label"><span class="label-text font-semibold">Program Title</span></label>
            <input bind:value={title} type="text" placeholder="e.g. Winter Strength Phase"
                   class="input input-bordered w-full" />
          </div>

          <div>
            <label class="label"><span class="label-text font-semibold">Start Date</span></label>
            <input bind:value={startDate} type="date" class="input input-bordered w-full" />
          </div>

          <div>
            <label class="label"><span class="label-text font-semibold">End Date</span></label>
            <input bind:value={endDate} type="date" class="input input-bordered w-full" />
          </div>

          <div class="sm:col-span-2">
            <label class="label"><span class="label-text font-semibold">Program Tag</span></label>
            <select bind:value={selectedTag} class="select select-bordered w-full">
              {#each tagOptions as tag}
                <option value={tag}>{tag}</option>
              {/each}
            </select>
            <p class="text-xs opacity-70 mt-1">{getSportHelp(selectedTag)}</p>
          </div>
        </div>



        <!-- Phase 1: Preferences Form -->
        {#if aiStep === 1}
          <div class="space-y-4 border-t border-base-300 pt-4">
            <h3 class="text-xl font-bold">User Preferences</h3>

            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label class="label"><span class="label-text">Days per week</span></label>
                <input bind:value={daysPerWeek} type="number" min="1" max="7" placeholder="e.g. 4"
                       class="input input-bordered w-full" />
              </div>

              <div>
                <label class="label"><span class="label-text">Preferred days</span></label>
                <input bind:value={preferredDays} type="text" placeholder="e.g. Mon, Wed, Fri"
                       class="input input-bordered w-full" />
              </div>

              <!-- Powerlifting only -->
              {#if selectedTag === 'Powerlifting'}
                <div>
                  <label class="label"><span class="label-text">Squat days / wk</span></label>
                  <input bind:value={squatDays} type="number" min="0" max="7" placeholder="e.g. 2"
                         class="input input-bordered w-full" />
                </div>
                <div>
                  <label class="label"><span class="label-text">Bench days / wk</span></label>
                  <input bind:value={benchDays} type="number" min="0" max="7" placeholder="e.g. 3"
                         class="input input-bordered w-full" />
                </div>
                <div>
                  <label class="label"><span class="label-text">Deadlift days / wk</span></label>
                  <input bind:value={deadliftDays} type="number" min="0" max="7" placeholder="e.g. 1"
                         class="input input-bordered w-full" />
                </div>
              {/if}

              <!-- Bodybuilding only -->
              {#if selectedTag === 'Bodybuilding'}
                <div class="sm:col-span-2">
                  <label class="label"><span class="label-text">Weak points</span></label>
                  <textarea bind:value={weakPointsInput} rows="2" class="textarea textarea-bordered w-full"
                            placeholder="Comma or line separated, e.g. chest, triceps, calves"></textarea>
                </div>
                <div>
                  <label class="label"><span class="label-text">Bodyweight</span></label>
                  <input bind:value={bodyweight} type="text" class="input input-bordered w-full"
                         placeholder="e.g. 172 lbs or 78 kg" />
                </div>
                <div>
                  <label class="label"><span class="label-text">Years of experience</span></label>
                  <input bind:value={yearsOfExperience} type="number" min="0" class="input input-bordered w-full"
                         placeholder="e.g. 3" />
                </div>
              {/if}

              <div class="sm:col-span-2">
                <label class="label"><span class="label-text">Favorite movements</span></label>
                <input bind:value={favoriteMovements} type="text" class="input input-bordered w-full"
                       placeholder="Comma-separated list" />
              </div>

              <div class="sm:col-span-2">
                <label class="label"><span class="label-text">Describe your training goals or needs</span></label>
                <textarea bind:value={userGoals} class="textarea textarea-bordered w-full" rows="3"
                          placeholder={selectedTag === 'Powerlifting'
                              ? "e.g. I want tempo bench for form and paused squats for control."
                              : "e.g. Emphasize upper chest and rear delts; bring up calves; avoid shoulder aggravation."}></textarea>
              </div>
            </div>

            <div class="flex justify-end">
            </div>
          </div>
        {/if}

        <!-- Phase 2: Outline -->
        {#if aiStep === 2}
          <div class="space-y-4 border-t border-base-300 pt-4">
            <div class="flex items-center justify-between">
              <h3 class="text-xl font-bold">Proposed Microcycle Outline</h3>

              {#if outline}
                <div class="hidden sm:flex items-center gap-2">
                  <span class="badge badge-ghost">{outline.Microcycle?.length || 0} days/wk</span>
                  <div class="flex flex-wrap gap-1">
                    {#each outline.Microcycle as d}
                      <span class="badge badge-ghost">{d.Day}</span>
                    {/each}
                  </div>
                </div>
              {/if}
            </div>

            {#if outline}
              {#if outline.Summary}
                <div class="alert alert-info shadow-sm"><span class="text-sm">{outline.Summary}</span></div>
              {/if}

              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                {#each outline.Microcycle as d, i}
                  <div class="card bg-base-100 shadow-sm hover:shadow-md transition-all duration-200 hover:-translate-y-0.5">
                    <div class="card-body p-4">
                      <div class="flex items-center justify-between">
                        <div class="flex items-center gap-3">
                          <div class="avatar placeholder">
                            <div class="bg-primary/10 text-primary w-10 rounded-full font-semibold flex items-center justify-center">
                              {d.Day?.[0] ?? "?"}
                            </div>
                          </div>
                          <div>
                            <div class="text-xs uppercase opacity-60">{d.Day}</div>
                            <div class="font-semibold leading-tight">{d.Focus}</div>
                          </div>
                        </div>
                        <span class="badge badge-outline">{i + 1}</span>
                      </div>

                      <div class="divider my-3"></div>

                      <div class="space-y-4">
                        <div>
                          <div class="text-xs uppercase opacity-60 mb-1">Main lifts</div>
                          {#if d.MainLifts?.length}
                            <div class="flex flex-wrap gap-2">
                              {#each d.MainLifts as ml}
                                <span class="badge badge-primary badge-outline">{ml}</span>
                              {/each}
                            </div>
                          {:else}
                            <span class="text-sm opacity-60">-</span>
                          {/if}
                        </div>

                        <div>
                          <div class="text-xs uppercase opacity-60 mb-1">Accessories</div>
                          {#if d.Accessories?.length}
                            <ul class="list-disc ml-6 text-sm leading-6">
                              {#each d.Accessories as acc}
                                <li>{acc}</li>
                              {/each}
                            </ul>
                          {:else}
                            <span class="text-sm opacity-60">-</span>
                          {/if}
                        </div>
                      </div>
                    </div>
                  </div>
                {/each}
              </div>

              <!-- Redo section (added back) -->
              <div class="mt-4 card bg-base-100 border border-base-300">
                <div class="card-body p-4">
                  <label class="label">
                    <span class="label-text">Suggest changes (optional)</span>
                  </label>
                  <textarea
                    bind:value={redoFeedback}
                    placeholder="e.g. Move Secondary Deadlift to Monday; shift Tempo Bench to Saturday; reduce overlap before deadlifts."
                    class="textarea textarea-bordered w-full"
                    rows="3"
                  ></textarea>
                  <div class="mt-3 flex flex-wrap gap-2">
                  </div>
                </div>
              </div>
            {/if}
          </div>
        {/if}

        <!-- AI Response / Logs -->
        {#if isAiLoading}
          <div class="flex items-center space-x-2">
            <span class="loading loading-spinner text-primary"></span>
            <span>Generating with AI...</span>
          </div>
        {:else if aiResponse}
          <div class="bg-base-100 p-3 rounded border border-base-300 max-h-96 overflow-auto text-sm">
            <pre>{typeof aiResponse === 'string' ? aiResponse : JSON.stringify(aiResponse, null, 2)}</pre>
          </div>
        {/if}
      </div>

      <!-- Footer -->
      <div class="p-4 border-t border-base-300 bg-base-100 flex justify-between">
        <button on:click={close} class="btn btn-ghost">Cancel</button>

        <div class="flex gap-2">
          {#if aiStep === 0}
            <button on:click={createProgram} class="btn btn-success">Create</button>
            <button on:click={createWithAi} class="btn btn-primary">Create with AI</button>
          {:else if aiStep === 1}
            <button on:click={sendPreferences} disabled={preferencesSubmitted || isAiLoading} class="btn btn-primary">
              Submit Preferences
            </button>
          {:else if aiStep === 2}
            <button on:click={redoOutline} class="btn btn-outline" disabled={isAiLoading || !redoFeedback.trim()}>
                Redo Outline
            </button>
            <button on:click={generateFirstWeek} disabled={!preferencesSubmitted || week1Generated || isAiLoading} class="btn btn-primary">
              Generate Week 1
            </button>
          {:else if aiStep === 3}
            <button on:click={generateNextWeek} disabled={!week1Generated || remainingWeeksGenerated || isAiLoading} class="btn btn-primary">
              Continue Weeks
            </button>
          {:else if aiStep >= 4}
            <button on:click={close} class="btn btn-primary">Complete</button>
          {/if}
        </div>
      </div>
    </div>
  </div>
{/if}
