import { fetchLastWeekActivityMinutes, fetchTodaysActivities } from '$lib/activityStore.js';

export const ssr = false;

export async function load({fetch, params}){
    fetchTodaysActivities(fetch);
    fetchLastWeekActivityMinutes(fetch);
    // const lastWeekActivityMinutes = await fetchLastWeekActivityMinutes(fetch)
    // return{
    //     lastWeekActivityMinutes
    // }
}