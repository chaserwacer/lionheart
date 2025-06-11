import { writable } from 'svelte/store';

//const initialTheme = localStorage.getItem('theme') || 'default';
const initialTheme = localStorage.content

export const theme = writable(initialTheme || 'default');

theme.subscribe(value => {
  localStorage.setItem('theme', value);
});
