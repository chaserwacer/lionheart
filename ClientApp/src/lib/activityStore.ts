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
        tonnage: 0
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

        const url = 'api/activity/getactivities?start=' + currentDate + '&end=' + currentDate
        const response = await fetch(url);

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

export async function fetchLastWeekActivityMinutes(fetch: { (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (arg0: string): any; }) {
    try {
        const endDate = new Date();
        const startDate = new Date();
        startDate.setDate(endDate.getDate() - 7);

        // Convert to strings in the year-month-day format
        const endString = endDate.toISOString().slice(0, 10); // "YYYY-MM-DD"
        const startString = startDate.toISOString().slice(0, 10); // "YYYY-MM-DD"


        const url = 'api/activity/getactivityminutes?start=' + startString + '&end=' + endString;
        const response = await fetch(url);

        if (response.ok) {
            const data = await response.json();
            lastWeeksActivityMinutes = data
        } else {
            console.error('Failed to get activity data');
        }
    }
    catch {
        console.error('Error fetching activity data')
    }
}