import { bootUserDto, fetchBootUserDto } from '$lib/stores';

export async function load({ fetch, params, url}) {
    await fetchBootUserDto(fetch)
    
}
