import { bootUserDto, fetchBootUserDto } from '$lib/stores/stores';

export const ssr = false;
export const prerender = true;

export async function load({ fetch, params, url }) {
    await fetchBootUserDto(fetch)

}
