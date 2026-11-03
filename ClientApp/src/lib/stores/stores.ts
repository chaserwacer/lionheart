import { writable } from 'svelte/store';

export const bootUserDto = writable({
    name: null as string | null,
    hasCreatedProfile: false
});

/** Bumped by a page after a write, so sibling pages know to refetch. */
export const pageUpdate = writable<Date>(new Date());

const PROFILE_KEY = 'lionheart.profileCreated';

/**
 * Whether this account has a profile, remembered locally.
 *
 * The API has no route that answers it: `/api/user/has-created-profile` was deleted with the old
 * user endpoints, and nothing GET-shaped reads `LionheartUsers`. `POST /api/user/create-profile`
 * answers 409 when one already exists, so the fact is learnable — but only by attempting a write.
 * Remembering the answer per account keeps the athlete out of the profile form on every load; a
 * fresh device asks once and settles it on the 409. See the report: this wants a real read route.
 */
function profileRemembered(email: string): boolean {
    if (typeof localStorage === 'undefined') return false;
    try {
        return localStorage.getItem(`${PROFILE_KEY}:${email}`) === 'true';
    } catch {
        return false;
    }
}

export function rememberProfileCreated(email: string): void {
    if (typeof localStorage === 'undefined') return;
    try {
        localStorage.setItem(`${PROFILE_KEY}:${email}`, 'true');
    } catch {
        // A blocked store only costs the athlete the profile form once more.
    }
}

type ManageInfo = { email?: string };

/**
 * Reads who is signed in. `/manage/info` is Identity's own route and answers 401 when nobody is,
 * which is the only signal the current API gives.
 */
export async function fetchBootUserDto(
    fetchFn: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response> = fetch
) {
    try {
        const response = await fetchFn('/manage/info');
        if (!response.ok) {
            bootUserDto.set({ name: null, hasCreatedProfile: false });
            return;
        }

        const info = (await response.json()) as ManageInfo;
        const email = info.email ?? '';
        bootUserDto.set({
            name: email === '' ? null : email,
            hasCreatedProfile: profileRemembered(email)
        });
    } catch (error) {
        console.error('Error reading the signed-in user:', error);
        bootUserDto.set({ name: null, hasCreatedProfile: false });
    }
}
