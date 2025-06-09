import { writable } from "svelte/store"

export type Activity = {
    activityID: '',
    userID: '',
    dateTime: '',
    timeInMinutes: '',
    caloriesBurned: 0,
    name: '',
    userSummary: '',
    accumulatedFatigue: 0,
    difficultyRating: 0,
    engagementRating: 0,
    externalVariablesRating: 0,
    runWalkDetails: {
        activityID: '',
        distance: 0.0,
        elevationGain: 0,
        averagePaceInSeconds: 0,
        MileSplitsInSeconds: [],
        runType: ''
    },
    liftDetails: {
        activityID: '',
        liftFocus: '',
        liftType: '',
        tonnage: 0,
        quadSets: 0,
        hamstringSets: 0,
        bicepSets: 0,
        tricepSets: 0,
        shoulderSets: 0,
        chestSets: 0,
        backSets: 0
    },
    rideDetails: {
        activityID: '',
        distance: 0.0,
        elevationGain: 0,
        averagePower: 0,
        averageSpeed: 0,
        rideType: ''
    }
}


export const todaysActivities = writable([])
export let lastWeeksActivityMinutes = 0

export async function fetchTodaysActivities(fetch: { (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (arg0: string): any; }) {
    try {
        const today = new Date();
        const year = today.getFullYear();
        const month = String(today.getMonth() + 1).padStart(2, '0');
        const day = String(today.getDate()).padStart(2, '0');
        const currentDate = `${year}-${month}-${day}`;

        // New endpoint: GET to /api/activity/get-activities?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD
        const url = `api/activity/get-activities?startDate=${encodeURIComponent(currentDate)}&endDate=${encodeURIComponent(currentDate)}`;
        const response = await fetch(url, {
            method: "GET",
            headers: { "Content-Type": "application/json" }
        });

        if (response.ok) {
            const data = await response.json();
            todaysActivities.set(data);
        } else {
            console.error('Failed to get todays activities');
        }
    }
    catch {
        console.error('Error fetching todays activities')
    }
}

export async function fetchActivityMinutes(end: string, fetch: { (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (arg0: string): any; }) {
    try {
        const start = new Date(end);
        start.setDate(start.getDate() - 7)
        const startString = start.toISOString().slice(0, 10);

        // New endpoint: GET to /api/activity/get-activity-minutes?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD
        const url = `api/activity/get-activity-minutes?startDate=${encodeURIComponent(startString)}&endDate=${encodeURIComponent(end)}`;
        const response = await fetch(url, {
            method: "GET",
            headers: { "Content-Type": "application/json" }
        });

        if (response.ok) {
            const data = await response.json();
            return data;
        } else {
            console.error('Failed to get activity data');
        }
    }
    catch {
        console.error('Error fetching activity data')
    }
}

export async function fetchActivities(start: string, end: string, fetch: { (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (arg0: string): any; }) {
    try {
        // New endpoint: GET to /api/activity/get-activities?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD
        const url = `api/activity/get-activities?startDate=${encodeURIComponent(start)}&endDate=${encodeURIComponent(end)}`;
        const response = await fetch(url, {
            method: "GET",
            headers: { "Content-Type": "application/json" }
        });

        if (response.ok) {
            const data = await response.json();
            return data;
        } else {
            console.error("Failed to get activities");
        }
    } catch {
        console.error("Error fetching activities");
    }
}

export async function fetchActivityRatio(end: string, fetch: { (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (arg0: string): any; }) {
    try {
        const start = new Date(end);;
        start.setDate(start.getDate() - 28);
        const startString = start.toISOString().slice(0, 10);

        // New endpoint: GET to /api/activity/get-activity-type-ratio?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD
        const url = `api/activity/get-activity-type-ratio?startDate=${encodeURIComponent(startString)}&endDate=${encodeURIComponent(end)}`;
        const response = await fetch(url, {
            method: "GET",
            headers: { "Content-Type": "application/json" }
        });

        if (response.ok) {
            const data = await response.json();
            return data
        } else {
            console.error("Failed to get activity data");
        }
    } catch {
        console.error("Error fetching activity data");
    }
}

export type MuscleSetsDto = {
    quadSets: 0,
    hamstringSets: 0,
    bicepSets: 0,
    tricepSets: 0,
    shoulderSets: 0,
    chestSets: 0,
    backSets: 0
}

export async function fetchWeeklyMuscleSetsDto(end: string, fetch: { (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (arg0: string): any; }) {
    try {
        const start = new Date(end);;
        start.setDate(start.getDate() - 7);
        const startString = start.toISOString().slice(0, 10);

        // New endpoint: GET to /api/activity/get-muscle-sets?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD
        const url = `api/activity/get-muscle-sets?startDate=${encodeURIComponent(startString)}&endDate=${encodeURIComponent(end)}`;
        const response = await fetch(url, {
            method: "GET",
            headers: { "Content-Type": "application/json" }
        });

        if (response.ok) {
            const data = await response.json();
            return data
        } else {
            console.error("Failed to get activity data");
        }
    } catch {
        console.error("Error fetching activity data");
    }
}