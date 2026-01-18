import { writable } from 'svelte/store';

export const bootUserDto = writable({
    name: '',
    hasCreatedProfile: false

});

export const pageUpdate = writable<Date>()



export async function fetchBootUserDto(fetch: { (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (arg0: string): any; }) {
    try {
        // Old: 'api/user/GetBootUserDto'
        // New endpoint: 'api/user/has-created-profile'
        const response = await fetch('/api/user/has-created-profile');
        if (response.ok) {
            const data = await response.json();
            bootUserDto.set(data);
        } else {
            const errorText = await response.text();
            console.error(`Failed to fetch BootUserDto: ${response.status} ${response.statusText} - ${errorText}`);
        }
    } catch (error) {
        console.error('Error fetching BootUserDto:', error);
    }
}




