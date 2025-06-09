import { writable } from 'svelte/store';

export const bootUserDto = writable({
    name: '',
    hasCreatedProfile: false

});

export const pageUpdate = writable<Date>()

export const wellnessStateDate = writable('');


export async function fetchBootUserDto(fetch: { (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (arg0: string): any; }) {
    try {
        // Old: 'api/user/GetBootUserDto'
        // New endpoint: 'api/user/has-created-profile'
        const response = await fetch('/api/user/has-created-profile');
        if (response.ok) {
            const data = await response.json();
            bootUserDto.set(data);
        } else {
            const errorText = await response.text();
            console.error(`Failed to fetch BootUserDto: ${response.status} ${response.statusText} - ${errorText}`);
        }
    } catch (error) {
        console.error('Error fetching BootUserDto:', error);
    }
}

export const todaysWellnessState = writable({
    motivationScore: 1,
    stressScore: 1,
    moodScore: 1,
    energyScore: 1,
    overallScore: 1,
    date: null
})

export type WellnessState = {
    motivationScore: number,
    stressScore: number,
    moodScore: number,
    energyScore: number,
    overallScore: number,
    date: string
}

export async function fetchTodaysWellnessState(fetch: { (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (arg0: string): any; }) {
    try {
        const today = new Date();
        const year = today.getFullYear();
        const month = String(today.getMonth() + 1).padStart(2, '0');
        const day = String(today.getDate()).padStart(2, '0');
        const currentDate = `${year}-${month}-${day}`;

        // Old: 'api/user/getwellnessstate?date=' + currentDate
        // New endpoint: 'api/wellness/get?date=' + currentDate
        const url = '/api/wellness/get?date=' + currentDate;
        const response = await fetch(url);

        if (response.ok) {
            const data = await response.json();
            todaysWellnessState.set(data);
        } else {
            console.error('Failed to get todays Wellness State');
        }
    }
    catch {
        console.error('Error fetching todays wellness state')
    }
}



