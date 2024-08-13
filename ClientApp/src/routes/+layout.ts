import { bootUserDto, fetchBootUserDto } from '$lib/stores';
import { fetchTodaysWellnessState } from '$lib/stores'

export async function load({ fetch, params, url }) {
    await fetchBootUserDto(fetch)
    await fetchTodaysWellnessState(fetch)
    let advice = ''

    await fetch('https://api.adviceslip.com/advice')
        .then(response => response.json())
        .then(data => {
            advice = data.slip.advice;
        });
    return {
        post:{
            advice
        }
        
    }
}
