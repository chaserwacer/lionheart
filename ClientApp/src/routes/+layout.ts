import { fetchBootUserDto } from '$lib/stores/stores';

export const ssr = false;
export const prerender = false;

export async function load({ fetch }) {
    await fetchBootUserDto(fetch);
}
