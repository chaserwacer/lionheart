import {fetchTodaysWellnessState, todaysWellnessState} from '$lib/stores'

export async function load({ fetch, params, url}) {
    await fetchTodaysWellnessState(fetch)
    
}