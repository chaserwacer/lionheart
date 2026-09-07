import { sveltekit } from '@sveltejs/kit/vite';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vitest/config';

export default defineConfig({
	plugins: [tailwindcss(), sveltekit()],
	server: {
		proxy: {
			'/api': {
			  target: 'http://localhost:7025',
			  changeOrigin: true,
			  ws: true
			},
			'/swagger': 'http://localhost:7025',
			'/login': 'http://localhost:7025',
			'/register': 'http://localhost:7025',
			// Identity's own routes; `/manage/info` is how the app reads who is signed in.
			'/manage': 'http://localhost:7025'
		}
	},
	test: {
		include: ['src/**/*.{test,spec}.{js,ts}']
	}
});
