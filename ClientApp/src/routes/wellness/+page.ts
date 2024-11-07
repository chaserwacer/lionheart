import {fetchTodaysWellnessState, todaysWellnessState} from '$lib/stores'

export async function load({ fetch, params, url}) {
    await fetchTodaysWellnessState(fetch)
    const response = await fetch('api/user/GetLastXWellnessStatesGraphData?xDays=7');
    if (!response.ok) {
        console.error('Failed to get todays Wellness State');
    }
    const data = await response.json(); 
        const { scores, dates } = data;

    return {
        post:{
            pastWeekScoreList: scores,
            pastWeekDateList: dates
        }
    }
}
