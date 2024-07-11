import { fetchBootUserDto } from '$lib/stores';

export async function load() {
    await fetchBootUserDto();
    // No need to return `bootUser` explicitly, SvelteKit will handle it.
}
