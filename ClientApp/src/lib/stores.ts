import { writable } from 'svelte/store';

export const bootUserDto = writable({
    name: '', 
    hasCreatedProfile: false

});

export async function fetchBootUserDto(fetch: { (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (arg0: string): any; }) {
    try {
        const response = await fetch('api/user/GetBootUserDto');
        if (response.ok) {
            const data = await response.json();
            bootUserDto.set(data);
            // console.log("Fetch Boot in store: data is:", data)
        } else {
            console.error('Failed to fetch BootUserDto');
        }
    } catch (error) {
        console.error('Error fetching BootUserDto:', error);
    }
}
