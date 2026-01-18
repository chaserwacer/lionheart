import { writable, derived } from 'svelte/store';

const isBrowser = typeof window !== 'undefined';

// Get system preference
function getSystemTheme() {
  if (!isBrowser) return 'bumblebee';
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'bumblebee';
}

// Get initial theme: 'system', 'light', or 'dark'
function getInitialPreference() {
  if (!isBrowser) return 'system';
  return localStorage.getItem('theme-preference') || 'system';
}

// Store for user preference ('system', 'light', or 'dark')
export const themePreference = writable(getInitialPreference());

// Store for system theme
export const systemTheme = writable(getSystemTheme());

// Derived store for actual theme to apply
export const theme = derived(
  [themePreference, systemTheme],
  ([$preference, $system]) => {
    if ($preference === 'system') {
      return $system;
    }
    return $preference;
  }
);

// Listen for system theme changes
if (isBrowser) {
  const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
  mediaQuery.addEventListener('change', (e) => {
    systemTheme.set(e.matches ? 'dark' : 'light');
  });

  // Persist preference
  themePreference.subscribe(value => {
    localStorage.setItem('theme-preference', value);
  });

  // Apply theme to document
  theme.subscribe(value => {
    document.documentElement.setAttribute('data-theme', value);
  });
}

// Helper to cycle through themes
export function toggleTheme() {
  themePreference.update(current => {
    if (current === 'system') return 'bumblebee';
    if (current === 'bumblebee') return 'dark';
    return 'system';
  });
}
