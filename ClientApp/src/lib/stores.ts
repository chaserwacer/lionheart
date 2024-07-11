import { writable } from 'svelte/store';

export const bootUserDto = writable({
    Name: null,
    HasCreatedProfile: false
});

export async function fetchBootUserDto() {
    try {
        const response = await fetch('/api/User/GetBootUserDtoAsync');
        if (response.ok) {
            const data = await response.json();
            bootUserDto.set(data);
        } else {
            console.error('Failed to fetch BootUserDto');
        }
    } catch (error) {
        console.error('Error fetching BootUserDto:', error);
    }
}
