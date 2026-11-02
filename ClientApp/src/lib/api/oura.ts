// The Oura read routes, typed by hand rather than through the generated ApiClient.
//
// Everything Oura measures reaches the client inside one JSON column per document, which NSwag
// renders as `any`, so the generated client would type none of this. These declarations are the
// wire shape from docs/oura-integration.md, and they are the only place it is written down on this
// side.
//
// Absence is null throughout, and that is Oura's own answer rather than a gap: the ring is off the
// finger for stretches, and it reports no value for a score it could not compute. Never render a
// null as 0 — see `score()` below.

export type OuraDayView = {
  day: string;
  activity: OuraActivityReading | null;
  readiness: OuraReadinessReading | null;
  sleep: OuraSleepReading | null;
  spo2: OuraSpo2Reading | null;
  stress: OuraStressReading | null;
  resilience: OuraResilienceReading | null;
};

export type OuraActivityReading = {
  score: number | null;
  contributors: {
    meetDailyTargets: number | null;
    moveEveryHour: number | null;
    recoveryTime: number | null;
    stayActive: number | null;
    trainingFrequency: number | null;
    trainingVolume: number | null;
  };
  steps: number;
  activeCalories: number;
  totalCalories: number;
  targetCalories: number;
  equivalentWalkingDistanceMeters: number;
  metersToTarget: number;
  targetMeters: number;
  inactivityAlerts: number;
  averageMet: number;
  lowActivityMetMinutes: number;
  mediumActivityMetMinutes: number;
  highActivityMetMinutes: number;
  sedentaryMetMinutes: number;
  restingTimeSeconds: number;
  sedentaryTimeSeconds: number;
  lowActivityTimeSeconds: number;
  mediumActivityTimeSeconds: number;
  highActivityTimeSeconds: number;
  nonWearTimeSeconds: number;
};

export type OuraReadinessReading = {
  score: number | null;
  temperatureDeviation: number | null;
  temperatureTrendDeviation: number | null;
  contributors: {
    activityBalance: number | null;
    bodyTemperature: number | null;
    hrvBalance: number | null;
    previousDayActivity: number | null;
    previousNight: number | null;
    recoveryIndex: number | null;
    restingHeartRate: number | null;
    sleepBalance: number | null;
    sleepRegularity: number | null;
  };
};

export type OuraSleepReading = {
  score: number | null;
  contributors: {
    deepSleep: number | null;
    efficiency: number | null;
    latency: number | null;
    remSleep: number | null;
    restfulness: number | null;
    timing: number | null;
    totalSleep: number | null;
  };
};

export type OuraSpo2Reading = {
  averageSpo2Percentage: number | null;
  breathingDisturbanceIndex: number | null;
};

export type OuraStressReading = {
  stressHighSeconds: number | null;
  recoveryHighSeconds: number | null;
  daySummary: string | null;
};

export type OuraResilienceReading = {
  level: string;
  contributors: {
    sleepRecovery: number | null;
    daytimeRecovery: number | null;
    stress: number | null;
  };
};

/** What a sync run did, per collection — including what Oura sent that could not be read. */
export type OuraSyncReport = {
  collections: {
    collection: string;
    received: number;
    stored: number;
    rejected: string[];
  }[];
  stored: number;
  rejected: number;
};

/** A score for display: the number, or an em dash where Oura has none. */
export function score(value: number | null | undefined): string {
  return value === null || value === undefined ? "—" : value.toString();
}

/** `yyyy-MM-dd`, which is what the day routes take and what `day` comes back as. */
export function ouraDay(date: Date): string {
  return date.toISOString().split("T")[0];
}

/** One day, or null when the ring recorded nothing for it (the route answers 404). */
export async function fetchOuraDay(day: string): Promise<OuraDayView | null> {
  const response = await fetch(`/api/oura/days/${day}`);

  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Oura day ${day} failed: ${response.status}`);

  return (await response.json()) as OuraDayView;
}

/** Every day in the range the ring recorded anything for, oldest first. */
export async function fetchOuraDays(from: string, to: string): Promise<OuraDayView[]> {
  const response = await fetch(
    `/api/oura/days?${new URLSearchParams({ from, to }).toString()}`,
  );

  if (!response.ok) throw new Error(`Oura days ${from}..${to} failed: ${response.status}`);

  return (await response.json()) as OuraDayView[];
}

export async function syncOura(from: string, to: string): Promise<OuraSyncReport> {
  const response = await fetch("/api/oura/sync", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ from, to }),
  });

  if (!response.ok) throw new Error(`Oura sync failed: ${response.status}`);

  return (await response.json()) as OuraSyncReport;
}
