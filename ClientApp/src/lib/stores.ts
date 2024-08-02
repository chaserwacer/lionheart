import { writable } from 'svelte/store';

export const bootUserDto = writable({
    name: '',
    hasCreatedProfile: false

});


export async function fetchBootUserDto(fetch: { (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (arg0: string): any; }) {
    try {
        const response = await fetch('api/user/GetBootUserDto');
        if (response.ok) {
            const data = await response.json();
            bootUserDto.set(data);
            // console.log("Fetch Boot in store: data is:", data)
        } else {
            console.error('Failed to fetch BootUserDto');
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

        const url = 'api/user/getwellnessstate?date=' + currentDate
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

