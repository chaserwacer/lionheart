import { writable } from 'svelte/store';

const isBrowser = typeof window !== 'undefined';
const initialTheme = isBrowser ? (localStorage.getItem('theme') || 'bumblebee') : 'bumblebee';

export const theme = writable(initialTheme);

if (isBrowser) {
  theme.subscribe(value => {
    localStorage.setItem('theme', value);
  });
}
