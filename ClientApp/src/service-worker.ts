/// <reference types="@sveltejs/kit" />
/// <reference no-default-lib="true"/>
/// <reference lib="esnext" />
/// <reference lib="webworker" />

import { base, build, files, version } from '$service-worker';

const worker = self as unknown as ServiceWorkerGlobalScope;

const cacheName = `lionheart-${version}`;

/**
 * The app is a client-rendered SPA behind adapter-static's fallback document, so every
 * route renders from this one shell. Caching it is what makes a home-screen launch work
 * with no network.
 */
const appShellUrl = `${base}/`;

worker.addEventListener('install', (event) => {
	event.waitUntil(precacheAppShell().then(() => worker.skipWaiting()));
});

worker.addEventListener('activate', (event) => {
	event.waitUntil(deleteStaleCaches().then(() => worker.clients.claim()));
});

worker.addEventListener('fetch', (event) => {
	const { request } = event;
	if (request.method !== 'GET') return;

	const url = new URL(request.url);
	if (url.origin !== worker.location.origin) return;

	// Training, wellness and chat data is per-user and must never be served stale.
	if (isBackendRequest(url)) return;

	if (request.mode === 'navigate') {
		event.respondWith(respondWithAppShell());
		return;
	}

	event.respondWith(respondFromCacheFirst(request));
});

async function precacheAppShell(): Promise<void> {
	const cache = await caches.open(cacheName);

	// `build` and `files` are content-hashed, so the HTTP cache can only ever hand back the right
	// bytes for them. The shell is not: fetched normally it can come from the HTTP cache still
	// holding the previous deploy's document, which points at script hashes this version replaced.
	await Promise.all([
		cache.addAll([...build, ...files]),
		fetch(appShellUrl, { cache: 'reload' }).then((response) => cache.put(appShellUrl, response))
	]);
}

async function deleteStaleCaches(): Promise<void> {
	const keys = await caches.keys();
	await Promise.all(keys.filter((key) => key !== cacheName).map((key) => caches.delete(key)));
}

function isBackendRequest(url: URL): boolean {
	// `/manage` is Identity's own surface, and `/manage/info` is how the app learns who is signed
	// in. Caching that answer would keep serving the previous account's identity after a logout.
	return ['/api/', '/login', '/register', '/manage', '/swagger'].some((prefix) =>
		url.pathname.startsWith(prefix)
	);
}

async function respondWithAppShell(): Promise<Response> {
	const cache = await caches.open(cacheName);
	const cachedShell = await cache.match(appShellUrl);

	// Refresh the shell in the background so a new deploy is picked up on the next launch. Bypasses
	// the HTTP cache for the reason precacheAppShell does.
	const networkShell = fetch(appShellUrl, { cache: 'reload' })
		.then((response) => {
			if (response.ok) cache.put(appShellUrl, response.clone());
			return response;
		})
		.catch(() => undefined);

	return cachedShell ?? (await networkShell) ?? Response.error();
}

async function respondFromCacheFirst(request: Request): Promise<Response> {
	const cache = await caches.open(cacheName);
	const cached = await cache.match(request);
	if (cached) return cached;

	try {
		const response = await fetch(request);
		if (response.ok) cache.put(request, response.clone());
		return response;
	} catch {
		return Response.error();
	}
}

export {};
