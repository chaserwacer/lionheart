import { fetchLastWeekActivityMinutes } from '$lib/activityStore.js';

export const ssr = false;

export async function load({fetch, params}){
    const lastWeekActivityMinutes = await fetchLastWeekActivityMinutes(fetch)
    return{
        lastWeekActivityMinutes
    }
}